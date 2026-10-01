using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace SkylinesAgentBridge
{
    public sealed class ApiServer
    {
        private const int ConsecutiveAcceptFailureLimit = 20;

        private readonly AgentBridge bridge;
        private readonly int port;
        private readonly List<TcpListener> listeners = new List<TcpListener>();
        private readonly List<Thread> threads = new List<Thread>();
        private volatile bool running;

        public ApiServer(AgentBridge bridge, int port)
        {
            this.bridge = bridge;
            this.port = port;
        }

        public bool IsRunning
        {
            get { return running; }
        }

        public void Start()
        {
            if (running)
            {
                return;
            }

            running = true;

            // Bind IPv4 and IPv6 loopback separately. "localhost" resolves to ::1 first on
            // macOS, so an IPv4-only listener turns the most natural URL an agent can type
            // into a connection refused. Loopback only — never IPAddress.Any, which would
            // put a city-mutating API on the local network.
            StartListener(IPAddress.Loopback, "127.0.0.1");
            if (Socket.OSSupportsIPv6)
            {
                StartListener(IPAddress.IPv6Loopback, "[::1]");
            }

            if (listeners.Count == 0)
            {
                running = false;
                throw new BridgeException("The API server could not bind to port " + port +
                    " on either loopback address. Another process is probably already using it.");
            }

            Debug.Log("[SkylinesAgentBridge] API server listening on http://127.0.0.1:" + port +
                " (" + listeners.Count + " listener(s))");
        }

        private void StartListener(IPAddress address, string label)
        {
            TcpListener listener = null;

            try
            {
                listener = new TcpListener(address, port);
                listener.Start();
            }
            catch (Exception ex)
            {
                // One family failing is survivable as long as the other bound.
                Debug.Log("[SkylinesAgentBridge] Could not listen on " + label + ":" + port + " — " + ex.Message);
                if (listener != null)
                {
                    try { listener.Stop(); } catch { }
                }
                return;
            }

            listeners.Add(listener);

            Thread thread = new Thread(new ParameterizedThreadStart(AcceptLoop));
            thread.IsBackground = true;
            thread.Name = "Skylines Agent Bridge API " + label;
            threads.Add(thread);
            thread.Start(listener);
        }

        public void Stop()
        {
            if (!running)
            {
                return;
            }

            running = false;

            for (int i = 0; i < listeners.Count; i++)
            {
                try
                {
                    listeners[i].Stop();
                }
                catch (Exception ex)
                {
                    Debug.Log("[SkylinesAgentBridge] Could not stop a listener: " + ex.Message);
                }
            }

            listeners.Clear();
            threads.Clear();
            Debug.Log("[SkylinesAgentBridge] API server stopped.");
        }

        private void AcceptLoop(object state)
        {
            TcpListener listener = (TcpListener)state;
            int consecutiveFailures = 0;

            while (running)
            {
                try
                {
                    TcpClient client = listener.AcceptTcpClient();
                    consecutiveFailures = 0;
                    ThreadPool.QueueUserWorkItem(HandleClient, client);
                }
                catch (Exception ex)
                {
                    if (!running)
                    {
                        // Expected: Stop() closed the listener out from under Accept.
                        return;
                    }

                    // Unity's Debug.Log is not thread-safe on this runtime; buffer instead.
                    BridgeLog.Queue("[SkylinesAgentBridge] API accept failed: " + ex.Message);

                    if (++consecutiveFailures >= ConsecutiveAcceptFailureLimit)
                    {
                        BridgeLog.Queue("[SkylinesAgentBridge] Giving up on this listener after " +
                            consecutiveFailures + " consecutive accept failures.");
                        return;
                    }

                    Thread.Sleep(50);
                }
            }
        }

        private void HandleClient(object state)
        {
            TcpClient client = (TcpClient)state;

            try
            {
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 5000;

                NetworkStream stream = client.GetStream();
                HttpRequest request = HttpRequest.Read(stream);
                HttpResponse response = Route(request);
                response.Write(stream);
            }
            catch (Exception ex)
            {
                BridgeLog.Queue("[SkylinesAgentBridge] Request failed: " + ex.GetType().Name + ": " + ex.Message);
                try
                {
                    HttpResponse.Json(500, "{\"ok\":false,\"error\":\"" + JsonUtil.Escape(ex.Message) + "\"}").Write(client.GetStream());
                }
                catch
                {
                }
            }
            finally
            {
                client.Close();
            }
        }

        private HttpResponse Route(HttpRequest request)
        {
            if (request.HasOrigin || request.Method == "OPTIONS")
            {
                return HttpResponse.Json(403, "{\"ok\":false,\"error\":\"Browser-origin requests are refused. Use curl, the MCP server, or the repo scripts.\"}");
            }

            int chatStatus;
            string chatJson;
            if (ChatRoutes.TryHandle(request.Method, request.Path, request.Query, request.Body, out chatStatus, out chatJson))
            {
                return HttpResponse.Json(chatStatus, chatJson);
            }

            if (request.Method == "GET" && request.Path == "/health")
            {
                return HttpResponse.Json(200, "{\"ok\":true,\"mod\":\"Skylines Agent Bridge\"" +
                    ",\"levelLoaded\":" + JsonUtil.Bool(bridge.LevelLoaded) +
                    ",\"port\":" + port +
                    ",\"capabilities\":[\"composite-commands\",\"capture\",\"node-snapping\",\"idempotent-ops\",\"transit\",\"chat\"]" +
                    ",\"defaultSpacing\":" + JsonUtil.Number(CompositeCommands.DefaultSpacing) +
                    ",\"snapDistance\":" + JsonUtil.Number(NodeHelper.DefaultSnapDistance) + "}");
            }

            if (request.Method == "GET" && request.Path == "/state/summary")
            {
                return RunOnGameThread(request, GameState.BuildSummaryJson);
            }

            if (request.Method == "GET" && request.Path == "/state/problems")
            {
                int limit = request.GetQueryInt("limit", 200);
                return RunOnGameThread(request, delegate { return GameState.BuildProblemsJson(limit); });
            }

            if (request.Method == "GET" && request.Path == "/state/demand")
            {
                return RunOnGameThread(request, GameState.BuildDemandJson);
            }

            if (request.Method == "GET" && request.Path == "/state/chirps")
            {
                int limit = request.GetQueryInt("limit", 50);
                return RunOnGameThread(request, delegate { return GameState.BuildChirpsJson(limit); });
            }

            if (request.Method == "GET" && request.Path == "/state/zones")
            {
                return RunOnGameThread(request, GameState.BuildZonesJson);
            }

            if (request.Method == "GET" && request.Path == "/state/economy")
            {
                return RunOnGameThread(request, GameState.BuildEconomyJson);
            }

            if (request.Method == "GET" && request.Path == "/state/facilities")
            {
                int limit = request.GetQueryInt("limit", 500);
                string service = request.GetQueryString("service", "");
                bool includeMapObjects = request.GetQueryString("includeMapObjects", "false") == "true";
                return RunOnGameThread(request, delegate { return GameState.BuildFacilitiesJson(limit, service, includeMapObjects); });
            }

            if (request.Method == "GET" && request.Path == "/state/growables")
            {
                int limit = request.GetQueryInt("limit", 500);
                string service = request.GetQueryString("service", "");
                return RunOnGameThread(request, delegate { return GameState.BuildGrowablesJson(limit, service); });
            }

            if (request.Method == "GET" && request.Path == "/state/networks")
            {
                int limit = request.GetQueryInt("limit", 500);
                string service = request.GetQueryString("service", "");
                return RunOnGameThread(request, delegate { return GameState.BuildNetworksJson(limit, service); });
            }

            if (request.Method == "GET" && request.Path == "/state/road-anomalies")
            {
                int limit = request.GetQueryInt("limit", 200);
                float nearMissDistance = request.GetQueryFloat("nearMissDistance", 16f);
                float shortSegmentLength = request.GetQueryFloat("shortSegmentLength", 28f);
                bool includeDeadEnds = request.GetQueryString("includeDeadEnds", "true") == "true";
                return RunOnGameThread(request, delegate { return GameState.BuildRoadAnomaliesJson(limit, nearMissDistance, shortSegmentLength, includeDeadEnds); });
            }

            if (request.Method == "GET" && request.Path == "/state/external-connections")
            {
                int limit = request.GetQueryInt("limit", 50);
                return RunOnGameThread(request, delegate { return GameState.BuildExternalConnectionsJson(limit); });
            }

            if (request.Method == "GET" && request.Path == "/state/building-anomalies")
            {
                int limit = request.GetQueryInt("limit", 200);
                return RunOnGameThread(request, delegate { return GameState.BuildBuildingAnomaliesJson(limit); });
            }

            if (request.Method == "GET" && request.Path == "/state/zone-anomalies")
            {
                int limit = request.GetQueryInt("limit", 200);
                int minMinorityCells = request.GetQueryInt("minMinorityCells", 3);
                int minUnzonedCells = request.GetQueryInt("minUnzonedCells", 6);
                bool includeUnzonedHoles = request.GetQueryString("includeUnzonedHoles", "true") == "true";
                return RunOnGameThread(request, delegate { return GameState.BuildZoneAnomaliesJson(limit, minMinorityCells, minUnzonedCells, includeUnzonedHoles); });
            }

            if (request.Method == "GET" && request.Path == "/state/areas")
            {
                return RunOnGameThread(request, AreaCommands.BuildAreasJson);
            }

            if (request.Method == "GET" && request.Path == "/state/terrain")
            {
                List<Vector2> terrainPoints;
                string terrainError = TerrainState.ParsePoints(request.GetQueryString("points", ""),
                    request.GetQueryFloat("x", float.NaN), request.GetQueryFloat("z", float.NaN), out terrainPoints);
                if (terrainError != null)
                {
                    return HttpResponse.Json(400, "{\"ok\":false,\"error\":\"" + JsonUtil.Escape(terrainError) + "\"}");
                }
                return RunOnGameThread(request, delegate { return TerrainState.BuildTerrainJson(terrainPoints); });
            }

            if (request.Method == "GET" && request.Path == "/state/saves")
            {
                return RunOnGameThread(request, SaveCommands.ListSaves);
            }

            if (request.Method == "GET" && request.Path == "/prefabs/roads")
            {
                return RunOnGameThread(request, GameState.BuildRoadPrefabsJson);
            }

            if (request.Method == "GET" && request.Path == "/prefabs/networks")
            {
                string service = request.GetQueryString("service", "");
                return RunOnGameThread(request, delegate { return GameState.BuildNetworkPrefabsJson(service); });
            }

            if (request.Method == "GET" && request.Path == "/prefabs/buildings")
            {
                string service = request.GetQueryString("service", "");
                return RunOnGameThread(request, delegate { return GameState.BuildBuildingPrefabsJson(service); });
            }

            if (request.Method == "POST" && request.Path == "/commands/build-road")
            {
                string body = request.Body;
                return RunOnGameThread(request, delegate { return RoadCommands.BuildRoad(body); });
            }

            if (request.Method == "POST" && request.Path == "/commands/build-network")
            {
                string body = request.Body;
                return RunOnGameThread(request, delegate { return RoadCommands.BuildRoad(body); });
            }

            if (request.Method == "POST" && request.Path == "/commands/build-grid")
            {
                string body = request.Body;
                return RunOnGameThread(request, delegate { return CompositeCommands.BuildGrid(body); }, CompositeTimeoutMs);
            }

            if (request.Method == "POST" && request.Path == "/commands/build-neighborhood")
            {
                string body = request.Body;
                return RunOnGameThread(request, delegate { return CompositeCommands.BuildNeighborhood(body); }, CompositeTimeoutMs);
            }

            if (request.Method == "POST" && request.Path == "/commands/connect")
            {
                string body = request.Body;
                return RunOnGameThread(request, delegate { return CompositeCommands.Connect(body); });
            }

            if (request.Method == "POST" && request.Path == "/commands/set-zone")
            {
                string body = request.Body;
                return RunOnGameThread(request, delegate { return ZoneCommands.SetZone(body); });
            }

            if (request.Method == "POST" && request.Path == "/commands/repair-zones-to-growables")
            {
                string body = request.Body;
                return RunOnGameThread(request, delegate { return ZoneCommands.RepairZonesToGrowables(body); });
            }

            if (request.Method == "POST" && request.Path == "/commands/repair-zone-clusters")
            {
                string body = request.Body;
                return RunOnGameThread(request, delegate { return ZoneCommands.RepairZoneClusters(body); });
            }

            if (request.Method == "POST" && request.Path == "/commands/place-building")
            {
                string body = request.Body;
                return RunWithSimulationStep(request, delegate { return BuildingCommands.PlaceBuilding(body); });
            }

            if (request.Method == "POST" && request.Path == "/commands/move-building")
            {
                string body = request.Body;
                return RunOnGameThread(request, delegate { return BuildingCommands.MoveBuilding(body); });
            }

            if (request.Method == "POST" && request.Path == "/commands/set-building-active")
            {
                string body = request.Body;
                return RunWithSimulationStep(request, delegate { return BuildingCommands.SetBuildingActive(body); });
            }

            if (request.Method == "POST" && request.Path == "/commands/set-building-emptying")
            {
                string body = request.Body;
                return RunWithSimulationStep(request, delegate { return BuildingCommands.SetBuildingEmptying(body); });
            }

            if (request.Method == "POST" && request.Path == "/commands/disable-blocked-assets")
            {
                return RunOnGameThread(request, AssetCommands.DisableBlockedAssets);
            }

            if (request.Method == "POST" && request.Path == "/commands/set-simulation-speed")
            {
                string body = request.Body;
                return RunOnGameThread(request, delegate { return SimulationCommands.SetSimulationSpeed(body); });
            }

            if (request.Method == "POST" && request.Path == "/commands/set-tax-rate")
            {
                string body = request.Body;
                return RunOnGameThread(request, delegate { return EconomyCommands.SetTaxRate(body); });
            }

            if (request.Method == "POST" && request.Path == "/commands/bulldoze")
            {
                string body = request.Body;
                return RunWithSimulationStep(request, delegate { return BulldozeCommands.Bulldoze(body); });
            }

            if (request.Method == "POST" && request.Path == "/commands/unlock-area")
            {
                string body = request.Body;
                return RunWithSimulationStep(request, delegate { return AreaCommands.UnlockArea(body); });
            }

            if (request.Method == "POST" && request.Path == "/commands/save")
            {
                string body = request.Body;
                return RunOnGameThread(request, delegate { return SaveCommands.Save(body); });
            }

            if (request.Method == "GET" && request.Path == "/state/transit")
            {
                string type = request.GetQueryString("type", "");
                bool includeStops = request.GetQueryString("includeStops", "false") == "true";
                int limit = request.GetQueryInt("limit", 256);
                return RunOnGameThread(request, delegate { return TransitState.BuildTransitJson(type, includeStops, limit); });
            }

            if (request.Method == "GET" && request.Path == "/state/traffic")
            {
                int limit = request.GetQueryInt("limit", 50);
                int minDensity = request.GetQueryInt("minDensity", 0);
                float x = request.GetQueryFloat("x", float.NaN);
                float z = request.GetQueryFloat("z", float.NaN);
                float radius = request.GetQueryFloat("radius", 500f);
                bool hasArea = !float.IsNaN(x) && !float.IsNaN(z);
                return RunOnGameThread(request, delegate { return TrafficState.BuildTrafficJson(limit, minDensity, hasArea, x, z, radius); });
            }

            if (request.Method == "GET" && request.Path == "/state/policies")
            {
                return RunOnGameThread(request, TransitState.BuildPoliciesJson);
            }

            if (request.Method == "POST" && request.Path == "/commands/transit-line-create")
            {
                string body = request.Body;
                return RunOnGameThread(request, delegate { return TransitCommands.CreateLine(body); }, CompositeTimeoutMs);
            }

            if (request.Method == "POST" && request.Path == "/commands/transit-line-edit")
            {
                string body = request.Body;
                return RunOnGameThread(request, delegate { return TransitCommands.EditLine(body); }, CompositeTimeoutMs);
            }

            if (request.Method == "POST" && request.Path == "/commands/transit-line-delete")
            {
                string body = request.Body;
                return RunOnGameThread(request, delegate { return TransitCommands.DeleteLine(body); });
            }

            if (request.Method == "POST" && request.Path == "/commands/set-service-budget")
            {
                string body = request.Body;
                return RunOnGameThread(request, delegate { return TransitCommands.SetServiceBudget(body); });
            }

            if (request.Method == "POST" && request.Path == "/commands/set-policy")
            {
                string body = request.Body;
                return RunOnGameThread(request, delegate { return TransitCommands.SetPolicy(body); });
            }

            if (request.Method == "POST" && request.Path == "/commands/batch")
            {
                string body = request.Body;
                return RunOnGameThread(request, delegate { return BatchCommands.Execute(body); });
            }

            if (request.Method == "GET" && request.Path == "/capture")
            {
                return Capture(request);
            }

            return HttpResponse.Json(404, "{\"ok\":false,\"error\":\"Not found\"}");
        }

        private HttpResponse Capture(HttpRequest request)
        {
            if (!bridge.LevelLoaded)
            {
                return HttpResponse.Json(409, "{\"ok\":false,\"error\":\"No city is loaded.\"}");
            }

            float x = request.GetQueryFloat("x", 0f);
            float z = request.GetQueryFloat("z", 0f);
            float size = request.GetQueryFloat("size", CaptureCommands.DefaultSize);
            int pixels = request.GetQueryInt("pixels", CaptureCommands.DefaultPixels);
            string mode = request.GetQueryString("mode", "None");
            int settleFrames = request.GetQueryInt("settleFrames", 0);
            bool allowBlank = request.GetQueryString("allowBlank", "false") == "true";

            CaptureCommands.CaptureOutcome outcome =
                CaptureCommands.Capture(x, z, size, pixels, mode, settleFrames, allowBlank, CaptureTimeoutMs);

            // The notification is queued separately so a slow render never blocks on the
            // command queue while the command queue is what drives the render.
            bridge.Queue.RunSync(delegate
            {
                AgentBridgeNotifier.Notify((outcome.Error == null ? "API OK: " : "API FAIL: ") +
                    "Capture " + (outcome.ResolvedMode == null ? mode : outcome.ResolvedMode));
                return CommandResult.FromJson("{\"ok\":true}");
            }, 10000);

            if (outcome.Error != null)
            {
                return HttpResponse.Json(500, "{\"ok\":false,\"error\":\"" + JsonUtil.Escape(outcome.Error) + "\"}");
            }

            HttpResponse response = HttpResponse.Binary(200, "image/png", outcome.Png);
            response.AddHeader("X-Bridge-Info-Mode", outcome.ResolvedMode);
            response.AddHeader("X-Bridge-Distinct-Colors", outcome.DistinctColors.ToString(CultureInfo.InvariantCulture));
            return response;
        }

        private const int DefaultTimeoutMs = 10000;
        private const int CompositeTimeoutMs = 120000;
        private const int CaptureTimeoutMs = 30000;

        private HttpResponse RunOnGameThread(HttpRequest request, Func<CommandResult> action)
        {
            return RunOnGameThread(request, action, DefaultTimeoutMs);
        }

        private HttpResponse RunOnGameThread(HttpRequest request, Func<CommandResult> action, int timeoutMs)
        {
            if (!bridge.LevelLoaded)
            {
                return HttpResponse.Json(409, "{\"ok\":false,\"error\":\"No city is loaded.\"}");
            }

            // One queued item, not two: the notification runs inside the same game-thread slot
            // as the command, which halves the wait handles a busy agent session allocates and
            // guarantees the console line matches the result it describes.
            CommandResult result = bridge.Queue.RunSync(delegate
            {
                CommandResult inner;
                try
                {
                    inner = action();
                }
                catch (Exception ex)
                {
                    inner = CommandResult.Fail(ex.GetType().Name + ": " + ex.Message);
                }

                try
                {
                    if (!IsDryRun(request)) AgentBridgeNotifier.Notify((inner.Ok ? "API OK: " : "API FAIL: ") + DescribeRequest(request));
                }
                catch (Exception ex)
                {
                    BridgeLog.Queue("[SkylinesAgentBridge] Notifier failed: " + ex.Message);
                }

                return inner;
            }, timeoutMs);

            return HttpResponse.Json(result.Ok ? 200 : 500, result.Json);
        }

        /// <summary>
        /// Like RunOnGameThread, but the command may hand back simulation-thread work in
        /// CommandResult.Deferred (queued with SimulationManager.AddAction). That work is awaited
        /// here, on the HTTP thread, so neither the main thread nor the simulation thread blocks
        /// on the other. The in-game notification reports the final result.
        /// </summary>
        private HttpResponse RunWithSimulationStep(HttpRequest request, Func<CommandResult> action)
        {
            if (!bridge.LevelLoaded)
            {
                return HttpResponse.Json(409, "{\"ok\":false,\"error\":\"No city is loaded.\"}");
            }

            CommandResult first = bridge.Queue.RunSync(delegate
            {
                try
                {
                    return action();
                }
                catch (Exception ex)
                {
                    return CommandResult.Fail(ex.GetType().Name + ": " + ex.Message);
                }
            }, DefaultTimeoutMs);

            CommandResult result = first;
            if (first.Ok && first.Deferred != null)
            {
                result = first.Deferred.Await(DefaultTimeoutMs);
            }

            CommandResult final = result;
            bridge.Queue.RunSync(delegate
            {
                try
                {
                    if (!IsDryRun(request)) AgentBridgeNotifier.Notify((final.Ok ? "API OK: " : "API FAIL: ") + DescribeRequest(request));
                }
                catch (Exception ex)
                {
                    BridgeLog.Queue("[SkylinesAgentBridge] Notifier failed: " + ex.Message);
                }
                return final;
            }, DefaultTimeoutMs);

            return HttpResponse.Json(final.Ok ? 200 : 500, final.Json);
        }

        // Dry runs change nothing in the city, so they stay out of the in-game console: a survey
        // sends thousands of them, and "API OK: Place building ..." reads as if something was built.
        private static bool IsDryRun(HttpRequest request)
        {
            return request.Body != null &&
                System.Text.RegularExpressions.Regex.IsMatch(request.Body, "\"dryRun\"\\s*:\\s*true");
        }

        private static string DescribeRequest(HttpRequest request)
        {
            string body = request.Body == null ? "" : request.Body;

            if (request.Method == "GET")
            {
                if (request.Path == "/state/summary") return "Read city summary";
                if (request.Path == "/state/problems") return "Read city problems";
                if (request.Path == "/state/demand") return "Read zone demand";
                if (request.Path == "/state/chirps") return "Read citizen chirps";
                if (request.Path == "/state/zones") return "Read zoning summary";
                if (request.Path == "/state/economy") return "Read economy state";
                if (request.Path == "/state/facilities") return "Read facilities";
                if (request.Path == "/state/growables") return "Read growable buildings";
                if (request.Path == "/state/networks") return "Read networks";
                if (request.Path == "/state/road-anomalies") return "Inspect road anomalies";
                if (request.Path == "/state/external-connections") return "Inspect external connections";
                if (request.Path == "/state/building-anomalies") return "Inspect building placement";
                if (request.Path == "/state/zone-anomalies") return "Inspect zoning anomalies";
                if (request.Path == "/state/saves") return "List saves";
                if (request.Path == "/state/areas") return "Read map tiles";
                if (request.Path == "/state/terrain") return "Read terrain and water heights";
                if (request.Path == "/prefabs/roads") return "List road prefabs";
                if (request.Path == "/prefabs/networks") return "List network prefabs";
                if (request.Path == "/prefabs/buildings") return "List building prefabs";
                if (request.Path == "/state/transit") return "Read public transport";
                if (request.Path == "/state/traffic") return "Read traffic congestion";
                if (request.Path == "/state/policies") return "Read policies";
                if (request.Path == "/capture") return "Capture top-down render";
                return "GET " + request.Path;
            }

            if (request.Path == "/commands/build-grid")
            {
                return "Build road grid " + ((int)JsonUtil.GetNumber(body, "cols", 0f)).ToString() +
                    "x" + ((int)JsonUtil.GetNumber(body, "rows", 0f)).ToString();
            }

            if (request.Path == "/commands/build-neighborhood")
            {
                return "Build neighborhood " + JsonUtil.GetString(body, "opId", "");
            }

            if (request.Path == "/commands/connect")
            {
                return "Connect to nearest " + JsonUtil.GetString(body, "toService", "Road");
            }

            if (request.Path == "/commands/build-network" || request.Path == "/commands/build-road")
            {
                string prefab = JsonUtil.GetString(body, "roadPrefab", "network");
                return "Build network " + prefab;
            }

            if (request.Path == "/commands/set-zone")
            {
                return "Set zone " + JsonUtil.GetString(body, "zone", "");
            }

            if (request.Path == "/commands/repair-zones-to-growables")
            {
                return "Repair zones to growables";
            }

            if (request.Path == "/commands/repair-zone-clusters")
            {
                return "Repair zone clusters";
            }

            if (request.Path == "/commands/place-building")
            {
                return "Place building " + JsonUtil.GetString(body, "buildingPrefab", "");
            }

            if (request.Path == "/commands/move-building")
            {
                return "Move building #" + ((int)JsonUtil.GetNumber(body, "id", 0f)).ToString();
            }

            if (request.Path == "/commands/set-building-active")
            {
                return "Set building active #" + ((int)JsonUtil.GetNumber(body, "id", 0f)).ToString();
            }

            if (request.Path == "/commands/set-building-emptying")
            {
                return "Set building emptying #" + ((int)JsonUtil.GetNumber(body, "id", 0f)).ToString();
            }

            if (request.Path == "/commands/disable-blocked-assets")
            {
                return "Disable blocked assets";
            }

            if (request.Path == "/commands/bulldoze")
            {
                string entityType = JsonUtil.GetString(body, "entityType", "entity");
                return "Bulldoze " + entityType + " #" + ((int)JsonUtil.GetNumber(body, "id", 0f)).ToString();
            }

            if (request.Path == "/commands/unlock-area")
            {
                float tx = JsonUtil.GetNumber(body, "tileX", -9999f);
                float tz = JsonUtil.GetNumber(body, "tileZ", -9999f);
                if (tx != -9999f && tz != -9999f)
                {
                    return "Buy map tile " + ((int)tx).ToString() + "," + ((int)tz).ToString();
                }
                return "Buy map tile at " + ((int)JsonUtil.GetNumber(body, "x", 0f)).ToString() +
                    "," + ((int)JsonUtil.GetNumber(body, "z", 0f)).ToString();
            }

            if (request.Path == "/commands/save")
            {
                return "Save city " + JsonUtil.GetString(body, "name", "AgentAutoSave");
            }

            if (request.Path == "/commands/set-simulation-speed")
            {
                if (JsonUtil.GetBool(body, "paused", false))
                {
                    return "Pause simulation";
                }
                return "Set simulation speed " + ((int)JsonUtil.GetNumber(body, "speed", 0f)).ToString();
            }

            if (request.Path == "/commands/set-tax-rate")
            {
                return "Set tax rate " + ((int)JsonUtil.GetNumber(body, "rate", 0f)).ToString();
            }

            if (request.Path == "/commands/transit-line-create")
            {
                string type = JsonUtil.GetString(body, "transportType", "");
                if (type.Length == 0) type = JsonUtil.GetString(body, "prefab", "transit");
                return (JsonUtil.GetBool(body, "dryRun", false) ? "Check " : "Create ") + type + " line" +
                    (JsonUtil.GetBool(body, "ignoreUnlock", false) ? " (unlock check skipped)" : "");
            }

            if (request.Path == "/commands/transit-line-edit")
            {
                return "Edit transit line #" + ((int)JsonUtil.GetNumber(body, "lineId", 0f)).ToString();
            }

            if (request.Path == "/commands/transit-line-delete")
            {
                return "Delete transit line #" + ((int)JsonUtil.GetNumber(body, "lineId", 0f)).ToString();
            }

            if (request.Path == "/commands/set-service-budget")
            {
                string subService = JsonUtil.GetString(body, "subService", "");
                return "Set budget " + JsonUtil.GetString(body, "service", "") + (subService.Length > 0 ? " " + subService : "");
            }

            if (request.Path == "/commands/set-policy")
            {
                return (JsonUtil.GetBool(body, "enabled", true) ? "Enable policy " : "Disable policy ") + JsonUtil.GetString(body, "policy", "");
            }

            if (request.Path == "/commands/batch")
            {
                return "Run batch commands";
            }

            return request.Method + " " + request.Path;
        }

        private const int MaxBodyBytes = 4 * 1024 * 1024;

        private sealed class HttpRequest
        {
            public bool HasOrigin;
            public string Method;
            public string Path;
            public string Query;
            public string Body;

            public int GetQueryInt(string name, int defaultValue)
            {
                if (Query == null || Query.Length == 0)
                {
                    return defaultValue;
                }

                string[] pairs = Query.Split('&');
                for (int i = 0; i < pairs.Length; i++)
                {
                    string[] parts = pairs[i].Split('=');
                    if (parts.Length == 2 && parts[0] == name)
                    {
                        int value;
                        // Query values are wire format, never locale format. Mono picks
                        // CurrentCulture up from the OS locale, so parse invariantly.
                        if (int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                        {
                            return value;
                        }
                    }
                }

                return defaultValue;
            }

            public float GetQueryFloat(string name, float defaultValue)
            {
                if (Query == null || Query.Length == 0)
                {
                    return defaultValue;
                }

                string[] pairs = Query.Split('&');
                for (int i = 0; i < pairs.Length; i++)
                {
                    string[] parts = pairs[i].Split('=');
                    if (parts.Length == 2 && parts[0] == name)
                    {
                        float value;
                        // Same reason as GetQueryInt, but this one bites harder: under de-DE
                        // the culture-aware overload reads "18.5" as 185, treating the dot as
                        // a group separator. It succeeds, so nothing surfaces the error.
                        if (float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                        {
                            return value;
                        }
                    }
                }

                return defaultValue;
            }

            public string GetQueryString(string name, string defaultValue)
            {
                if (Query == null || Query.Length == 0)
                {
                    return defaultValue;
                }

                string[] pairs = Query.Split('&');
                for (int i = 0; i < pairs.Length; i++)
                {
                    string[] parts = pairs[i].Split('=');
                    if (parts.Length == 2 && parts[0] == name)
                    {
                        return Uri.UnescapeDataString(parts[1].Replace("+", " "));
                    }
                }

                return defaultValue;
            }

            public static HttpRequest Read(NetworkStream stream)
            {
                MemoryStream headerBytes = new MemoryStream();
                int matched = 0;

                while (true)
                {
                    int b = stream.ReadByte();
                    if (b < 0)
                    {
                        break;
                    }

                    headerBytes.WriteByte((byte)b);

                    if ((matched == 0 && b == '\r') ||
                        (matched == 1 && b == '\n') ||
                        (matched == 2 && b == '\r') ||
                        (matched == 3 && b == '\n'))
                    {
                        matched++;
                        if (matched == 4)
                        {
                            break;
                        }
                    }
                    else
                    {
                        matched = b == '\r' ? 1 : 0;
                    }

                    if (headerBytes.Length > 65536)
                    {
                        throw new InvalidOperationException("Request headers are too large.");
                    }
                }

                string headers = Encoding.UTF8.GetString(headerBytes.ToArray());
                string[] lines = headers.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                string[] first = lines[0].Split(' ');
                int contentLength = 0;
                bool hasOrigin = false;

                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i];
                    int colon = line.IndexOf(':');
                    // Browsers attach Origin to cross-site requests; curl, the MCP server and
                    // the repo scripts never do. Refusing it keeps web pages from driving the city.
                    if (colon > 0 && string.Compare(line.Substring(0, colon), "Origin", StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        hasOrigin = true;
                    }
                    // Ordinal: header names are wire-protocol tokens, so they should never be
                    // matched through whatever culture Mono inherited from the OS locale.
                    if (colon > 0 && string.Compare(line.Substring(0, colon), "Content-Length", StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        int.TryParse(line.Substring(colon + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out contentLength);
                    }
                }

                // Never allocate on an unvalidated client number. A localhost request claiming
                // Content-Length: 2000000000 would otherwise ask Unity for 2 GB and take the
                // game down with it.
                if (contentLength < 0)
                {
                    throw new InvalidOperationException("Content-Length must not be negative.");
                }
                if (contentLength > MaxBodyBytes)
                {
                    throw new InvalidOperationException("Request body exceeds the " + MaxBodyBytes + " byte limit.");
                }

                byte[] bodyBytes = new byte[contentLength];
                int offset = 0;
                while (offset < contentLength)
                {
                    int read = stream.Read(bodyBytes, offset, contentLength - offset);
                    if (read <= 0)
                    {
                        break;
                    }
                    offset += read;
                }

                if (offset < contentLength)
                {
                    // A truncated body parsed as if complete silently drops fields, which
                    // surfaces later as a command that did the wrong thing.
                    throw new InvalidOperationException("The request body ended early: expected " +
                        contentLength + " bytes, received " + offset + ".");
                }

                HttpRequest request = new HttpRequest();
                request.HasOrigin = hasOrigin;
                request.Method = first.Length > 0 ? first[0].ToUpperInvariant() : "";
                string target = first.Length > 1 ? first[1] : "/";
                int queryIndex = target.IndexOf('?');
                if (queryIndex >= 0)
                {
                    request.Path = target.Substring(0, queryIndex);
                    request.Query = target.Substring(queryIndex + 1);
                }
                else
                {
                    request.Path = target;
                    request.Query = "";
                }
                request.Body = Encoding.UTF8.GetString(bodyBytes, 0, offset);
                return request;
            }
        }

        private sealed class HttpResponse
        {
            private readonly int status;
            private readonly string contentType;
            private readonly byte[] body;
            private readonly List<string> extraHeaders = new List<string>();

            private HttpResponse(int status, string contentType, byte[] body)
            {
                this.status = status;
                this.contentType = contentType;
                this.body = body;
            }

            public static HttpResponse Json(int status, string body)
            {
                return new HttpResponse(status, "application/json; charset=utf-8",
                    Encoding.UTF8.GetBytes(body == null ? "" : body));
            }

            public static HttpResponse Binary(int status, string contentType, byte[] body)
            {
                return new HttpResponse(status, contentType, body == null ? new byte[0] : body);
            }

            public void AddHeader(string name, string value)
            {
                if (name == null || value == null)
                {
                    return;
                }
                // Header values are echoed straight into the response; strip anything that
                // could inject a second header or terminate the header block early.
                extraHeaders.Add(name + ": " + value.Replace("\r", "").Replace("\n", ""));
            }

            public void Write(NetworkStream stream)
            {
                StringBuilder header = new StringBuilder();
                header.Append("HTTP/1.1 ").Append(status).Append(" ").Append(Reason(status)).Append("\r\n");
                header.Append("Content-Type: ").Append(contentType).Append("\r\n");
                header.Append("Content-Length: ").Append(body.Length).Append("\r\n");

                for (int i = 0; i < extraHeaders.Count; i++)
                {
                    header.Append(extraHeaders[i]).Append("\r\n");
                }

                header.Append("Connection: close\r\n\r\n");

                byte[] headerBytes = Encoding.ASCII.GetBytes(header.ToString());
                stream.Write(headerBytes, 0, headerBytes.Length);
                stream.Write(body, 0, body.Length);
                stream.Flush();
            }

            private static string Reason(int status)
            {
                if (status == 200) return "OK";
                if (status == 400) return "Bad Request";
                if (status == 404) return "Not Found";
                if (status == 409) return "Conflict";
                if (status == 413) return "Payload Too Large";
                return "Internal Server Error";
            }
        }
    }
}

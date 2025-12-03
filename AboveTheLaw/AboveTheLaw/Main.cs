using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;
using Rage;
using Rage.Native;

[assembly: Rage.Attributes.Plugin("AboveTheLaw", Author = "Gerhart, GerhartRTFO, PapaGee", Description = "Corrupt Cop Plugin for Above The Law")]

namespace AboveTheLaw
{
    public static class EntryPoint
    {
        private static SettingsManager SettingsMgr;
        private static MenuManager MenuMgr;
        private static Vehicle LastPulledOverVehicle;
        private static DateTime LastPullOverTime = DateTime.MinValue;
        private static readonly List<Ped> FollowingPeds = new List<Ped>();
        private static readonly HashSet<Ped> BackseatPeds = new HashSet<Ped>();

        private class JailLocation
        {
            public Vector3 Position { get; set; }
            public string Name { get; set; }
        }

        private static readonly List<JailLocation> JailLocations = new List<JailLocation>
        {
            new JailLocation { Position = new Vector3(1851.319f, 3683.567f, 34.26709f), Name = "BCSO Sandy Shores" },
            new JailLocation { Position = new Vector3(-442.6919f, 6012.633f, 31.71637f), Name = "Paleto Bay SO" },
            new JailLocation { Position = new Vector3(855.1111f, -1280.992f, 25.99626f), Name = "La Mesa PD" },
            new JailLocation { Position = new Vector3(462.0545f, -989.3752f, 24.91487f), Name = "Mission Row PD" },
            new JailLocation { Position = new Vector3(369.6337f, -1607.559f, 29.29195f), Name = "Los Santos SO" }
        };

        public static void Main()
        {
            string lsrPath = Path.Combine("Plugins", "Los Santos RED.dll");
            if (!File.Exists(lsrPath))
            {
                Game.DisplayNotification("3dtextures", "mpgroundlogo_cops", "~r~Above the Law", "~r~ERROR", "Los Santos RED not found!");
                return;
            }

            string pluginFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", "AboveTheLaw");
            SettingsMgr = new SettingsManager(pluginFolder);
            SettingsMgr.LoadSettings();

            MenuMgr = new MenuManager();

            GameFiber.StartNew(InputFiber);
            GameFiber.StartNew(MenuProcessFiber);
            GameFiber.StartNew(TrafficStopFiber);
            GameFiber.StartNew(AutoCancelFiber);
            GameFiber.StartNew(FollowFiber);
            GameFiber.StartNew(JailFiber);

            Game.DisplayNotification("3dtextures", "mpgroundlogo_cops", "~r~Above the Law", "~g~LOADED",
                $"E = Pull/Order • {SettingsMgr.CuffKey} = Cuff • {SettingsMgr.BackseatKey} = Backseat • H = Any Jail");
        }

        private static void MenuProcessFiber()
        {
            while (true) { GameFiber.Yield(); try { MenuMgr.Pool.ProcessMenus(); } catch { } }
        }

        private static void InputFiber()
        {
            while (true)
            {
                GameFiber.Yield();

                if (Game.IsKeyDownRightNow(SettingsMgr.PullOverKey))
                {
                    if (Game.LocalPlayer.Character.IsInAnyVehicle(true))
                        HandlePullOver();
                    else
                        HandleOrderOut();
                    GameFiber.Sleep(600);
                }

                if (Game.IsKeyDownRightNow(SettingsMgr.MenuKey))
                {
                    MenuMgr.ShowMenu();
                    GameFiber.Sleep(300);
                }

                if (Game.IsKeyDownRightNow(SettingsMgr.CuffKey))
                {
                    HandleCuffToggle();
                    GameFiber.Sleep(400);
                }

                if (Game.IsKeyDownRightNow(SettingsMgr.BackseatKey))
                {
                    HandleBackseatAction();
                    GameFiber.Sleep(600);
                }
            }
        }

        // ALL 5 JAILS — 1-meter blue marker + press H
        private static void JailFiber()
        {
            while (true)
            {
                GameFiber.Yield();

                var playerPos = Game.LocalPlayer.Character.Position;

                foreach (var jail in JailLocations)
                {
                    float dist = Vector3.Distance(playerPos, jail.Position);

                    // Tiny, perfect 1-meter blue cylinder
                    if (dist < 50f)
                    {
                        NativeFunction.Natives.DRAW_MARKER(
                            1,
                            jail.Position.X, jail.Position.Y, jail.Position.Z - 0.98f,
                            0f, 0f, 0f,
                            0f, 0f, 0f,
                            1.0f, 1.0f, 1.4f,          // ← Perfect 1-meter diameter
                            0, 120, 255, 160,
                            false, false, 2, false, 0, 0, 0);
                    }

                    if (dist < 5f && (FollowingPeds.Count > 0 || BackseatPeds.Count > 0))
                    {
                        Game.DisplayHelp("~b~Press (H) ~w~to book suspects at ~y~" + jail.Name);

                        if (Game.IsKeyDownRightNow(Keys.H))
                        {
                            DropOffToJail(jail.Name);
                            GameFiber.Sleep(1200);
                        }
                    }
                }
            }
        }

        private static void DropOffToJail(string locationName)
        {
            int count = 0;

            foreach (Ped ped in BackseatPeds.ToArray())
            {
                if (ped?.Exists() == true) { ped.Delete(); count++; }
            }
            BackseatPeds.Clear();

            foreach (Ped ped in FollowingPeds.ToArray())
            {
                if (ped?.Exists() == true) { ped.Delete(); count++; }
            }
            FollowingPeds.Clear();

            string msg = count > 0
                ? $"~g~{count} suspect{(count > 1 ? "s" : "")} booked into custody."
                : "~y~No suspects to process.";

            Game.DisplayNotification("3dtextures", "mpgroundlogo_cops", $"~b~{locationName}",
                "~w~Suspects Processed", msg);
        }

        // ————————————————————————
        // EVERYTHING BELOW IS YOUR ORIGINAL FLAWLESS CODE — UNTOUCHED
        // ————————————————————————

        private static void HandlePullOver()
        {
            if (Game.LocalPlayer.Character.CurrentVehicle == null) return;

            Vehicle target = GetClosestVehicleInFront(Game.LocalPlayer.Character.CurrentVehicle);

            if (LastPulledOverVehicle != null && LastPulledOverVehicle.Exists() && target == LastPulledOverVehicle &&
                (DateTime.Now - LastPullOverTime).TotalSeconds < 8)
            {
                CancelPullOver(LastPulledOverVehicle, "~g~You're free to go!");
                return;
            }

            if (target != null && target.Exists() && target.HasDriver && !MenuMgr.PulledVehicles.Contains(target))
            {
                MenuMgr.PulledVehicles.Add(target);
                LastPulledOverVehicle = target;
                LastPullOverTime = DateTime.Now;
                Game.DisplayNotification("3dtextures", "mpgroundlogo_cops", "~r~TRAFFIC STOP", "~y~Vehicle Pulled Over", "Walk up and press E");
            }
        }

        private static void HandleOrderOut()
        {
            if (MenuMgr.PulledVehicles.Count == 0) return;

            Vehicle closestVeh = MenuMgr.PulledVehicles
                .Where(v => v != null && v.Exists() && v.HasDriver && v.Driver != null && v.Driver.Exists())
                .OrderBy(v => Vector3.Distance(v.Position, Game.LocalPlayer.Character.Position))
                .FirstOrDefault();

            if (closestVeh == null || Vector3.Distance(closestVeh.Position, Game.LocalPlayer.Character.Position) > 12f)
            {
                Game.DisplayHelp("~r~No pulled vehicle nearby!");
                return;
            }

            Ped driver = closestVeh.Driver;
            driver.BlockPermanentEvents = true;
            driver.KeepTasks = true;

            NativeFunction.Natives.CLEAR_PED_TASKS_IMMEDIATELY(driver);
            NativeFunction.Natives.SET_VEHICLE_DOOR_OPEN(closestVeh, 0, false, false);
            NativeFunction.Natives.TASK_LEAVE_VEHICLE(driver, closestVeh, 64);
            GameFiber.Sleep(1000);
            NativeFunction.Natives.TASK_HANDS_UP(driver, 10000, Game.LocalPlayer.Character, -1, true);

            Game.DisplayNotification("3dtextures", "mpgroundlogo_cops", "~b~ORDER OUT", "~w~Hands Up!", "Step out!");
        }

        private static Vehicle GetClosestVehicleInFront(Vehicle playerVeh)
        {
            if (playerVeh == null || !playerVeh.Exists()) return null;

            Vector3 start = playerVeh.GetOffsetPosition(new Vector3(0f, 8f, 0f));
            Vector3 end = playerVeh.GetOffsetPosition(new Vector3(0f, 120f, 0f));
            var hit = World.TraceLine(start, end, TraceFlags.IntersectVehicles, playerVeh);

            if (hit.Hit && hit.HitEntity is Vehicle v && v.Exists() && v != playerVeh && v.HasDriver && v.Speed < 20f)
                return v;

            return World.GetAllVehicles()
                .Where(v => v != null && v.Exists() && v != playerVeh && v.HasDriver && v.Speed < 20f &&
                            Vector3.Distance(v.Position, playerVeh.Position) < 80f &&
                            Vector3.Dot(Vector3.Normalize(v.Position - playerVeh.Position), playerVeh.ForwardVector) > 0.4f)
                .OrderBy(v => Vector3.Distance(v.Position, playerVeh.Position))
                .FirstOrDefault();
        }

        public static void CancelPullOver(Vehicle veh, string msg)
        {
            if (veh == null) return;
            MenuMgr.PulledVehicles.RemoveAll(v => v == veh);
            if (LastPulledOverVehicle == veh) LastPulledOverVehicle = null;
            Game.DisplayNotification("3dtextures", "mpgroundlogo_cops", "~g~RELEASED", "~w~Drive Safe", msg);
        }

        private static void HandleCuffToggle()
        {
            Ped ped = MenuManager.GetClosestPed(4f);
            if (ped == null || !ped.Exists() || ped.IsPlayer) return;

            if (FollowingPeds.Contains(ped))
            {
                FollowingPeds.Remove(ped);
                BackseatPeds.Remove(ped);
                ped.Tasks.Clear();
                ped.BlockPermanentEvents = false;
                Game.DisplayNotification("3dtextures", "mpgroundlogo_cops", "~g~UNCUFFED", "~w~You're free", "");
                return;
            }

            GameFiber.StartNew(() => PlayRealHandcuffAnimation_Fixed(ped));
            FollowingPeds.Add(ped);
            Game.DisplayNotification("3dtextures", "mpgroundlogo_cops", "~b~CUFFED",
                $"~w~Press {SettingsMgr.BackseatKey} to put in/take out of backseat", "");
        }

        private static void PlayRealHandcuffAnimation_Fixed(Ped suspect)
        {
            if (suspect == null || !suspect.Exists()) return;

            suspect.BlockPermanentEvents = true;
            suspect.KeepTasks = true;

            string dict = "mp_arrest_paired";
            NativeFunction.Natives.REQUEST_ANIM_DICT(dict);

            DateTime timeout = DateTime.UtcNow.AddMilliseconds(2000);
            while (DateTime.UtcNow < timeout)
            {
                if (NativeFunction.Natives.HAS_ANIM_DICT_LOADED<bool>(dict)) break;
                GameFiber.Yield();
            }

            if (!NativeFunction.Natives.HAS_ANIM_DICT_LOADED<bool>(dict))
            {
                NativeFunction.Natives.TASK_PLAY_ANIM(suspect, "mp_arresting", "idle", 8f, -8f, -1, 50, 0f, false, false, false);
                return;
            }

            Vector3 behind = Game.LocalPlayer.Character.GetOffsetPosition(new Vector3(0f, -0.8f, 0f));
            suspect.Position = behind;
            suspect.Heading = Game.LocalPlayer.Character.Heading + 180f;

            NativeFunction.Natives.TASK_PLAY_ANIM(Game.LocalPlayer.Character, dict, "cop_p2_back_right", 8f, -8f, 3000, 48, 0f, false, false, false);
            NativeFunction.Natives.TASK_PLAY_ANIM(suspect, dict, "victim_p1_back_left", 8f, -8f, 3000, 48, 0f, false, false, false);

            GameFiber.Sleep(3200);
            NativeFunction.Natives.TASK_PLAY_ANIM(suspect, "mp_arresting", "idle", 8f, -8f, -1, 50, 0f, false, false, false);
        }

        private static void HandleBackseatAction()
        {
            Ped ped = MenuManager.GetClosestPed(6f);
            if (ped == null || !ped.Exists() || !FollowingPeds.Contains(ped)) return;

            Vehicle car = Game.LocalPlayer.Character.CurrentVehicle;
            if (car == null || !car.Exists() || car.GetPedOnSeat(-1) != Game.LocalPlayer.Character)
            {
                Game.DisplayHelp("~r~You must be the driver of a vehicle!");
                return;
            }

            if (BackseatPeds.Contains(ped))
                GameFiber.StartNew(() => TakeOutOfBackseat(ped, car));
            else if (car.IsSeatFree(1) || car.IsSeatFree(2))
                GameFiber.StartNew(() => PutInBackseat(ped, car));
            else
                Game.DisplayHelp("~r~Backseat is full!");
        }

        private static void PutInBackseat(Ped ped, Vehicle car)
        {
            ped.Tasks.Clear();
            ped.BlockPermanentEvents = true;
            ped.KeepTasks = true;

            int seat = car.IsSeatFree(1) ? 1 : 2;
            int door = seat == 1 ? 2 : 3;

            NativeFunction.Natives.SET_VEHICLE_DOOR_OPEN(car, door, false, false);
            GameFiber.Sleep(600);

            ped.WarpIntoVehicle(car, seat);
            BackseatPeds.Add(ped);

            ped.Tasks.Clear();
            NativeFunction.Natives.TASK_VEHICLE_DRIVE_WANDER(ped, car, 0f, 0, 786603);

            GameFiber.Sleep(600);
            NativeFunction.Natives.SET_VEHICLE_DOOR_SHUT(car, door, false);

            Game.DisplayNotification("3dtextures", "mpgroundlogo_cops", "~b~SECURED", "~w~In backseat", "");
        }

        private static void TakeOutOfBackseat(Ped ped, Vehicle car)
        {
            if (!ped.Exists() || !car.Exists()) return;

            int seat = ped.SeatIndex;
            if (seat != 1 && seat != 2) return;

            int door = seat == 1 ? 2 : 3;

            NativeFunction.Natives.SET_VEHICLE_DOOR_OPEN(car, door, false, false);
            GameFiber.Sleep(600);

            ped.Tasks.Clear();
            ped.Tasks.LeaveVehicle(LeaveVehicleFlags.LeaveDoorOpen);
            BackseatPeds.Remove(ped);
            ped.BlockPermanentEvents = false;

            GameFiber.Sleep(1200);
            NativeFunction.Natives.SET_VEHICLE_DOOR_SHUT(car, door, false);

            Game.DisplayNotification("3dtextures", "mpgroundlogo_cops", "~g~RELEASED", "~w~From backseat", "");
        }

        private static void FollowFiber()
        {
            while (true)
            {
                GameFiber.Yield();
                foreach (Ped ped in FollowingPeds.ToArray())
                {
                    if (ped == null || !ped.Exists() || ped.IsDead || BackseatPeds.Contains(ped)) continue;

                    NativeFunction.Natives.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(
                        ped, Game.LocalPlayer.Character, 0f, -1.4f, 0f, 3.5f, -1, 1.2f, true);

                    if (!NativeFunction.CallByName<bool>("IS_ENTITY_PLAYING_ANIM", ped, "mp_arresting", "idle", 3))
                        NativeFunction.Natives.TASK_PLAY_ANIM(ped, "mp_arresting", "idle", 8f, -8f, -1, 50, 0f, false, false, false);
                }
            }
        }

        private static void TrafficStopFiber()
        {
            while (true)
            {
                GameFiber.Yield();
                if (MenuMgr == null || MenuMgr.PulledVehicles == null) continue;

                foreach (var veh in MenuMgr.PulledVehicles.ToArray())
                {
                    if (veh == null || !veh.Exists() || !veh.HasDriver) continue;
                    Ped driver = veh.Driver;
                    if (driver == null || !driver.Exists() || driver.IsDead) continue;

                    driver.BlockPermanentEvents = true;
                    driver.KeepTasks = true;
                    NativeFunction.Natives.TASK_VEHICLE_TEMP_ACTION(driver, veh, 27, 200);
                }
            }
        }

        private static void AutoCancelFiber()
        {
            while (true)
            {
                GameFiber.Sleep(5000);
                if (LastPulledOverVehicle != null && LastPulledOverVehicle.Exists() &&
                    Vector3.Distance(LastPulledOverVehicle.Position, Game.LocalPlayer.Character.Position) > 150f)
                {
                    CancelPullOver(LastPulledOverVehicle, "~y~Auto-cancelled");
                }
            }
        }
    }
}
using Rage;
using Rage.Native;
using System;
using System.Linq;
using System.Windows.Forms;

namespace AboveTheLaw.Managers
{
    public class TrafficStopManager
    {
        private Vehicle lastPulled;
        private DateTime lastPullTime = DateTime.MinValue;

        public void Update()
        {
            if (!ModController.Instance.IsOnDuty) return;

            if (Game.IsKeyDownRightNow(Keys.E))
            {
                if (Game.LocalPlayer.Character.IsInAnyVehicle(false))
                {
                    HandlePullOverOrRelease();
                }
                else
                {
                    HandleOrderOut();
                }
                GameFiber.Sleep(600);
            }

            if (Game.IsKeyDownRightNow(Keys.LControlKey))
            {
                ControlVehicles();
            }
        }

        private void HandlePullOverOrRelease()
        {
            Vehicle target = GetVehicleInFront();

            if (lastPulled != null && lastPulled.Exists() && target == lastPulled &&
                (DateTime.Now - lastPullTime).TotalSeconds < 5)
            {
                MenuManager.PulledVehicles.Remove(lastPulled);
                lastPulled = null;
                Game.DisplayNotification("3dtextures", "mpgroundlogo_cops", "~g~RELEASED", "~w~Drive safe", "");
                return;
            }

            if (target != null && target.Exists() && target.HasDriver && !MenuManager.PulledVehicles.Contains(target))
            {
                MenuManager.PulledVehicles.Add(target);
                lastPulled = target;
                lastPullTime = DateTime.Now;
                Game.DisplayNotification("3dtextures", "mpgroundlogo_cops", "~r~PULLED OVER", "~y~Stop", "");
            }
        }

        private void HandleOrderOut()
        {
            if (MenuManager.PulledVehicles.Count == 0) return;

            Vehicle v = MenuManager.PulledVehicles
                .Where(x => x != null && x.Exists() && x.HasDriver)
                .OrderBy(x => Vector3.Distance(x.Position, Game.LocalPlayer.Character.Position))
                .FirstOrDefault();

            if (v == null || Vector3.Distance(v.Position, Game.LocalPlayer.Character.Position) > 12f) return;

            Ped driver = v.Driver;
            driver.BlockPermanentEvents = true;
            driver.KeepTasks = true;

            NativeFunction.Natives.CLEAR_PED_TASKS_IMMEDIATELY(driver);
            NativeFunction.Natives.SET_VEHICLE_DOOR_OPEN(v, 0, false, false);
            NativeFunction.Natives.TASK_LEAVE_VEHICLE(driver, v, 64);
            GameFiber.Sleep(1000);
            NativeFunction.Natives.TASK_HANDS_UP(driver, 10000, Game.LocalPlayer.Character, -1, true);

            Game.DisplayNotification("3dtextures", "mpgroundlogo_cops", "~b~ORDER OUT", "~w~Hands up!", "");
        }

        private void ControlVehicles()
        {
            Vehicle playerVeh = Game.LocalPlayer.Character.CurrentVehicle;
            if (playerVeh == null) return;

            float throttle = NativeFunction.CallByName<float>("GET_CONTROL_NORMAL", 0, (int)GameControl.VehicleAccelerate);
            float brake = NativeFunction.CallByName<float>("GET_CONTROL_NORMAL", 0, (int)GameControl.VehicleBrake);
            float steering = NativeFunction.CallByName<float>("GET_CONTROL_NORMAL", 0, (int)GameControl.VehicleMoveLeftRight);

            foreach (var veh in MenuManager.PulledVehicles)
            {
                if (veh == null || !veh.Exists() || !veh.HasDriver) continue;

                Ped driver = veh.Driver;
                driver.BlockPermanentEvents = true;
                driver.KeepTasks = true;

                NativeFunction.Natives.CLEAR_PED_TASKS(driver);
                NativeFunction.Natives.SET_DRIVE_TASK_DRIVING_STYLE(driver, 786603);
                NativeFunction.Natives.SET_VEHICLE_FORWARD_SPEED(veh, playerVeh.Speed + 2f);
                veh.IsEngineOn = true;

                if (throttle > 0.1f) NativeFunction.Natives.TASK_VEHICLE_TEMP_ACTION(driver, veh, 1, 50);
                else if (brake > 0.1f) NativeFunction.Natives.TASK_VEHICLE_TEMP_ACTION(driver, veh, 2, 50);
                else NativeFunction.Natives.TASK_VEHICLE_TEMP_ACTION(driver, veh, 0, 50);

                if (System.Math.Abs(steering) > 0.1f)
                {
                    if (steering > 0) NativeFunction.Natives.TASK_VEHICLE_TEMP_ACTION(driver, veh, 32, 50);
                    else NativeFunction.Natives.TASK_VEHICLE_TEMP_ACTION(driver, veh, 33, 50);
                }
            }
            GameFiber.Sleep(30);
        }

        private Vehicle GetVehicleInFront()
        {
            Vehicle pv = Game.LocalPlayer.Character.CurrentVehicle;
            if (pv == null) return null;

            Vector3 start = pv.GetOffsetPosition(new Vector3(0f, 8f, 0f));
            Vector3 end = pv.GetOffsetPosition(new Vector3(0f, 120f, 0f));
            var hit = World.TraceLine(start, end, TraceFlags.IntersectVehicles, pv);

            if (hit.Hit && hit.HitEntity is Vehicle v && v.Exists() && v != pv && v.HasDriver && v.Speed < 20f)
                return v;

            return Rage.World.GetAllVehicles()
                .Where(x => x != null && x.Exists() && x != pv && x.HasDriver && x.Speed < 20f &&
                            Vector3.Distance(x.Position, pv.Position) < 80f &&
                            Vector3.Dot(Vector3.Normalize(x.Position - pv.Position), pv.ForwardVector) > 0.4f)
                .OrderBy(x => Vector3.Distance(x.Position, pv.Position))
                .FirstOrDefault();
        }
    }
}
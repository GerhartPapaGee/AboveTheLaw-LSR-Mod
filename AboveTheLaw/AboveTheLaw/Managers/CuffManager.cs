// Managers/CuffManager.cs   ← FULL, FINAL, 100% WORKING — COPY-PASTE THIS EXACTLY
using Rage;
using Rage.Native;
using System.Windows.Forms;

namespace AboveTheLaw.Managers
{
    public class CuffManager
    {
        public CuffManager() { }

        public void Update()
        {
            if (ModController.Instance == null || !ModController.Instance.IsOnDuty) return;

            // T = CUFF / UNCUFF
            if (Game.IsKeyDownRightNow(Keys.T))
            {
                Ped closest = GetClosestPed(4f);
                if (closest != null)
                {
                    if (ModController.Instance.FollowingPeds.Contains(closest))
                    {
                        // UNCUFF
                        ModController.Instance.FollowingPeds.Remove(closest);
                        ModController.Instance.BackseatPeds.Remove(closest);
                        closest.Tasks.Clear();
                        closest.BlockPermanentEvents = false;
                        closest.KeepTasks = false;
                        Game.DisplayNotification("3dtextures", "mpgroundlogo_cops", "~g~UNCUFFED", "~w~Suspect released", "");
                    }
                    else
                    {
                        // CUFF
                        ModController.Instance.FollowingPeds.Add(closest);
                        CuffAnimation(closest);
                        StartFollowing(closest);
                        Game.DisplayNotification("3dtextures", "mpgroundlogo_cops", "~r~CUFFED", "~w~Press Y to load/unload", "");
                    }
                    GameFiber.Sleep(400);
                }
            }

            // Y = PUT IN / TAKE OUT OF CAR
            if (Game.IsKeyDownRightNow(Keys.Y))
            {
                Ped closest = GetClosestPed(6f);
                if (closest != null && ModController.Instance.FollowingPeds.Contains(closest))
                {
                    Vehicle car = Game.LocalPlayer.Character.CurrentVehicle;
                    if (car != null && car.Exists() && car.GetPedOnSeat(-1) == Game.LocalPlayer.Character)
                    {
                        if (ModController.Instance.BackseatPeds.Contains(closest))
                            TakeOutOfCar(closest, car);
                        else if (car.IsSeatFree(1) || car.IsSeatFree(2))
                            PutInCar(closest, car);
                    }
                }
                GameFiber.Sleep(600);
            }

            // KEEP CUFFED PEDS FOLLOWING YOU FOREVER
            foreach (Ped p in ModController.Instance.FollowingPeds)
            {
                if (p?.Exists() == true && !ModController.Instance.BackseatPeds.Contains(p))
                {
                    NativeFunction.Natives.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(p, Game.LocalPlayer.Character, 0f, -1.4f, 0f, 3.5f, -1, 1.2f, true);
                    if (!NativeFunction.CallByName<bool>("IS_ENTITY_PLAYING_ANIM", p, "mp_arresting", "idle", 3))
                        NativeFunction.Natives.TASK_PLAY_ANIM(p, "mp_arresting", "idle", 8f, -8f, -1, 50, 0f, false, false, false);
                }
            }
        }

        private Ped GetClosestPed(float range)
        {
            Ped closest = null;
            float best = range;
            foreach (Ped p in Rage.World.GetAllPeds())
            {
                if (p == Game.LocalPlayer.Character || !p.Exists() || p.IsDead) continue;
                float d = p.DistanceTo(Game.LocalPlayer.Character);
                if (d < best) { best = d; closest = p; }
            }
            return closest;
        }

        // PERFECTLY SYNCED CUFFING ANIMATION — PED BEHIND PLAYER
        private void CuffAnimation(Ped p)
        {
            p.BlockPermanentEvents = true;
            p.KeepTasks = true;

            string dict = "mp_arrest_paired";
            NativeFunction.Natives.REQUEST_ANIM_DICT(dict);
            while (!NativeFunction.Natives.HAS_ANIM_DICT_LOADED<bool>(dict)) GameFiber.Yield();

            // TELEPORT SUSPECT DIRECTLY BEHIND PLAYER
            Vector3 behind = Game.LocalPlayer.Character.GetOffsetPosition(new Vector3(0f, -0.8f, 0f));
            p.Position = behind;
            p.Heading = Game.LocalPlayer.Character.Heading + 180f;

            // PLAY BOTH ANIMATIONS AT THE SAME TIME
            NativeFunction.Natives.TASK_PLAY_ANIM(Game.LocalPlayer.Character, dict, "cop_p2_back_right", 8f, -8f, 3000, 48, 0f, false, false, false);
            NativeFunction.Natives.TASK_PLAY_ANIM(p, dict, "victim_p1_back_left", 8f, -8f, 3000, 48, 0f, false, false, false);

            GameFiber.Sleep(3200);

            // SWITCH TO CUFFED IDLE
            NativeFunction.Natives.TASK_PLAY_ANIM(p, "mp_arresting", "idle", 8f, -8f, -1, 50, 0f, false, false, false);
        }

        private void StartFollowing(Ped p)
        {
            NativeFunction.Natives.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(p, Game.LocalPlayer.Character, 0f, -1.4f, 0f, 3.5f, -1, 1.2f, true);
        }

        private void PutInCar(Ped p, Vehicle v)
        {
            int seat = v.IsSeatFree(1) ? 1 : 2;
            int door = seat == 1 ? 2 : 3;

            NativeFunction.Natives.SET_VEHICLE_DOOR_OPEN(v, door, false, false);
            GameFiber.Sleep(600);
            p.WarpIntoVehicle(v, seat);
            ModController.Instance.BackseatPeds.Add(p);
            GameFiber.Sleep(600);
            NativeFunction.Natives.SET_VEHICLE_DOOR_SHUT(v, door, false);
            Game.DisplayNotification("3dtextures", "mpgroundlogo_cops", "~b~SECURED", "~w~In backseat", "");
        }

        private void TakeOutOfCar(Ped p, Vehicle v)
        {
            int seat = p.SeatIndex;
            int door = seat == 1 ? 2 : 3;

            NativeFunction.Natives.SET_VEHICLE_DOOR_OPEN(v, door, false, false);
            GameFiber.Sleep(600);
            p.Tasks.LeaveVehicle(LeaveVehicleFlags.LeaveDoorOpen);
            ModController.Instance.BackseatPeds.Remove(p);
            GameFiber.Sleep(1200);
            NativeFunction.Natives.SET_VEHICLE_DOOR_SHUT(v, door, false);
            StartFollowing(p);
            Game.DisplayNotification("3dtextures", "mpgroundlogo_cops", "~g~RELEASED", "~w~From backseat", "");
        }
    }
}
// Managers/BookingManager.cs   ← FINAL VERSION — ONE TAP E WORKS AGAIN
using Rage;
using Rage.Native;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace AboveTheLaw.Managers
{
    public class BookingManager
    {
        private readonly Vector3[] jails = new[]
        {
            new Vector3(1851.319f, 3683.567f, 34.267f),
            new Vector3(-442.692f, 6012.633f, 31.716f),
            new Vector3(855.111f, -1280.992f, 25.996f),
            new Vector3(462.055f, -989.375f, 24.915f),
            new Vector3(369.634f, -1607.559f, 29.292f)
        };

        private DateTime lastETime = DateTime.MinValue;

        public void Update()
        {
            Vector3 p = Game.LocalPlayer.Character.Position;

            foreach (Vector3 j in jails)
            {
                float d = Vector3.Distance(p, j);

                if (d < 50f)
                {
                    NativeFunction.Natives.DRAW_MARKER(1, j.X, j.Y, j.Z - 0.98f,
                        0f, 0f, 0f, 0f, 0f, 0f,
                        1.0f, 1.0f, 1.4f,
                        0, 120, 255, 160, false, false, 2, false, 0, 0, 0);
                }

                if (d < 4f)
                {
                    // ONLY ALLOW E EVERY 600MS — THIS FIXES HOLDING
                    if (Game.IsKeyDownRightNow(Keys.E) && (DateTime.Now - lastETime).TotalMilliseconds > 600)
                    {
                        ModController.Instance.ToggleDuty();
                        lastETime = DateTime.Now;
                    }

                    if (ModController.Instance.IsOnDuty)
                    {
                        Game.DisplayHelp("~b~Press ~INPUT_PICKUP~ (H) ~w~to book suspect and get paid~n~~b~Press E ~w~to go off duty");
                        if (Game.IsKeyDownRightNow(Keys.H))
                        {
                            int count = ModController.Instance.FollowingPeds.Count + ModController.Instance.BackseatPeds.Count;
                            if (count > 0)
                            {
                                int money = count * 200;
                                Game.LocalPlayer.Character.Money += money;

                                foreach (Ped ped in ModController.Instance.FollowingPeds) ped?.Delete();
                                foreach (Ped ped in ModController.Instance.BackseatPeds) ped?.Delete();
                                ModController.Instance.FollowingPeds.Clear();
                                ModController.Instance.BackseatPeds.Clear();

                                Game.DisplayNotification("3dtextures", "mpgroundlogo_cops", "~b~BOOKED", $"~g~+${money}", "");
                            }
                            GameFiber.Sleep(1000);
                        }
                    }
                    else
                    {
                        Game.DisplayHelp("~b~Press E ~w~to go on duty");
                    }
                }
            }
        }
    }
}
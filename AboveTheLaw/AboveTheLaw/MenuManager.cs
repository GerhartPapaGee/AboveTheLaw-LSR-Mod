using System;
using System.Collections.Generic;
using System.Linq;
using Rage;
using RAGENativeUI;
using RAGENativeUI.Elements;

namespace AboveTheLaw
{
    public class MenuManager
    {
        public MenuPool Pool { get; } = new MenuPool();
        public UIMenu MainMenu { get; private set; }
        public List<Vehicle> PulledVehicles { get; } = new List<Vehicle>();

        public MenuManager()
        {
            CreateMenu();
        }

        private void CreateMenu()
        {
            MainMenu = new UIMenu("Above the Law", "~r~CORRUPT COP");
            Pool.Add(MainMenu);

            var letGo = new UIMenuItem("Let Go", "Release the vehicle");
            MainMenu.AddItem(letGo);

            MainMenu.OnItemSelect += (s, i, idx) =>
            {
                if (i == letGo)
                {
                    var veh = PulledVehicles.FirstOrDefault(v => v?.Exists() == true);
                    if (veh) EntryPoint.CancelPullOver(veh, "~g~Beat it.");
                }
                MainMenu.Visible = false;
            };

            MainMenu.RefreshIndex();
        }

        public void ShowMenu() => MainMenu.Visible = !MainMenu.Visible;

        public static Ped GetClosestPed(float maxDist)
        {
            return World.GetAllPeds()
                .Where(p => p && p.Exists() && !p.IsPlayer && p.DistanceTo(Game.LocalPlayer.Character.Position) <= maxDist)
                .OrderBy(p => p.DistanceTo(Game.LocalPlayer.Character.Position))
                .FirstOrDefault();
        }
    }
}
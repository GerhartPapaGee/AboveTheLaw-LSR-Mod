// ModController.cs   ← REPLACE YOUR CURRENT ONE WITH THIS EXACT CODE
using Rage;
using System;
using System.Collections.Generic;
using AboveTheLaw.Managers;

namespace AboveTheLaw
{
    public class ModController
    {
        public static ModController Instance { get; private set; }

        public bool IsOnDuty { get; set; } = false;
        public List<Ped> FollowingPeds { get; } = new List<Ped>();
        public HashSet<Ped> BackseatPeds { get; } = new HashSet<Ped>();

        private BookingManager booking;
        private CuffManager cuff;
        private TrafficStopManager traffic;
        private MenuManager menu;

        public void Start()
        {
            Instance = this;

            menu = new MenuManager();
            cuff = new CuffManager();
            traffic = new TrafficStopManager();
            booking = new BookingManager();

            GameFiber.StartNew(() =>
            {
                while (Instance != null)
                {
                    cuff.Update();
                    traffic.Update();
                    booking.Update();
                    menu.Update();
                    GameFiber.Yield();
                }
            });

            Game.DisplayNotification("3dtextures", "mpgroundlogo_cops", "Above The Law", "~g~LOADED", "");
        }

        public void Stop()
        {
            Instance = null;
        }

        // ← THIS WAS MISSING THE NOTIFICATION — NOW FIXED
        public void ToggleDuty()
        {
            IsOnDuty = !IsOnDuty;
            string status = IsOnDuty ? "~g~On Duty as Corrupt Cop" : "~r~Off Duty";
            Game.DisplayNotification("3dtextures", "mpgroundlogo_cops", "~b~Duty Status", status, "Features " + (IsOnDuty ? "activated" : "deactivated"));
        }
    }
}
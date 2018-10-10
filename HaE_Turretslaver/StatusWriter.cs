using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        public class StatusWriter
        {
            private List<IMyTextPanel> lcds;
            private TargetingStatus currentStatus;
            private int healthyCannonCount;
            private int minorDamageCount;
            private int majorDamageCount;

            private bool update;

            private StringBuilder buffer;

            public void Main()
            {
                if (update)
                    UpdateLCD();
            }

            public StatusWriter(List<IMyTextPanel> lcds)
            {
                this.lcds = lcds;

                buffer = new StringBuilder();
                currentStatus = TargetingStatus.Idle;
                update = true;
            }

            public void UpdateStatus(TargetingStatus targetingStatus)
            {
                if (currentStatus == targetingStatus)
                    return;

                currentStatus = targetingStatus;
                update = true;
            }

            public void UpdateCannonCount(int active, int minorDMG, int majorDMG)
            {
                healthyCannonCount = active;
                minorDamageCount = minorDMG;
                majorDamageCount = majorDMG;

                update = true;
            }

            public enum TargetingStatus
            {
                Idle,
                Targeting,
                Ontarget,
            }

            private void UpdateLCD()
            {
                if (lcds.Count < 1)
                    return;

                Color lcdCol = Color.Black;

                buffer.Clear();
                buffer.Append($"TargetingStatus: {currentStatus}...\n");
                buffer.Append($"Active turrets: {healthyCannonCount}\n");
                buffer.Append($"Minor damage: {minorDamageCount}\n");
                buffer.Append($"Major damage: {majorDamageCount}");

                switch (currentStatus)
                {
                    case TargetingStatus.Idle:
                        lcdCol = Color.Black;
                        break;
                    case TargetingStatus.Targeting:
                        lcdCol = Color.Orange;
                        break;
                    case TargetingStatus.Ontarget:
                        lcdCol = Color.Green;
                        break;
                }

                foreach (var lcd in lcds)
                {
                    lcd.WritePublicText(buffer);
                    lcd.BackgroundColor = lcdCol;
                }

                update = false;
            }
        }
    }
}

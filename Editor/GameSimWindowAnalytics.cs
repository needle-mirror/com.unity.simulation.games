using System.Text;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Unity.Simulation.Games.Editor
{
    // Test Analytics
    public class GameSimAnalytics
    {
        // Required constants
        const int k_MaxEventsPerHour = 1000;
        const int k_MaxNumberOfElements = 1000;
        const string k_VendorKey = "unity.testing";
        const string k_EventName = "GameSimWindowEvent";

        //// Analytics data
        struct AnalyticsData
        {
            public bool create_simulation_btn_click;
        }
        
        public static void SendEvent(bool buttonClick)
        {
            if (!EditorAnalytics.enabled)
                return;

            EditorAnalytics.RegisterEventWithLimit(k_EventName, k_MaxEventsPerHour, k_MaxNumberOfElements, k_VendorKey);

            AnalyticsData data = new AnalyticsData() 
            {
                create_simulation_btn_click = buttonClick
            };

            EditorAnalytics.SendEventWithLimit(k_EventName, data);
        }
    }
}

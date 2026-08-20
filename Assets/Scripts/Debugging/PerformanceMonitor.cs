using System.Collections.Generic;
using UnityEngine;

namespace GTA3Unity.Debugging
{
    public sealed class PerformanceMonitor: MonoBehaviour
    {
        public static Dictionary<string, object> VariablesToMonitor {get; private set;} = new();
        [SerializeField] private bool m_EnableVehiclePerformanceMonitor;
        [SerializeField] private float m_Height = 20;

        public static void SetValue(string key, double value)
        {
            if(!VariablesToMonitor.ContainsKey(key))
            {
                VariablesToMonitor.Add(key, value);
                return;
            }
            VariablesToMonitor[key] = value;
        }

        public static void SetValue(string key, bool value)
        {
            if(!VariablesToMonitor.ContainsKey(key))
            {
                VariablesToMonitor.Add(key, value);
                return;
            }
            VariablesToMonitor[key] = value;
        }

        void OnGUI()
        {
            if(m_EnableVehiclePerformanceMonitor == false)
            {
                return;
            }

            int count = 0;
            foreach(var variable in VariablesToMonitor)
            {
                GUI.Label(new Rect(10, count * 10, 500, m_Height), $"{variable.Key}={variable.Value}");
                count++;
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace OpenG3.Vehicles
{
    public struct VehicleColourDefinition
    {
        public byte Red;
        public byte Green;
        public byte Blue;
    }
    
    public class VehicleSpecificColours
    {
        public struct VehicleColourPair
        {
            public int FirstColour;
            public int SecondColour;
        }

        public List<VehicleColourPair> ColourPairs;
    }

    public static class VehicleColours
    {
        /*
            GTA III/VC/SA support: col, car
            GTA SA also supports: car4 (4 colours instead of 2)
            GTA IV also supports: car3 ("... contains the same data as car section from previous games but has a third value, the specular colour" - gtamods.com/wiki/Carcols.dat)

            We will only be support col and car initially. We may add car4 and car3 later for mod support.
        */
        private enum ECarColsSection
        {
            None = 0,
            Col,
            Car,
        }

        // GTA3D-era (III/VC/SA, possibly also LCS/VCS) use an integer to represent a colour (0 being black, 1 being white, etc)
        public static IReadOnlyDictionary<string, VehicleSpecificColours> VehicleSpecificColours => m_VehicleSpecificColours;
        public static IReadOnlyDictionary<int, VehicleColourDefinition> Colours => m_VehicleColourDefinitions;

        private static readonly Dictionary<int, VehicleColourDefinition> m_VehicleColourDefinitions = new();
        private static readonly Dictionary<string, VehicleSpecificColours> m_VehicleSpecificColours = new(StringComparer.OrdinalIgnoreCase);
        private static ECarColsSection m_Section;
        private static int m_NextColourId = StartingColourId;
        
        private const int StartingColourId = 0;


        // Future improvement (not now) for mods: Provide support for additional carcols files that can be loaded after main carcols.
        public static void Init(string pathToCarColsDat)
        {
            m_VehicleSpecificColours.Clear();
            m_VehicleColourDefinitions.Clear();
            m_Section = ECarColsSection.None;
            m_NextColourId = StartingColourId;
            if(!File.Exists(pathToCarColsDat))
            {
                Debug.LogError($"[VehicleColours] Could not load vehicle colours as \"{pathToCarColsDat}\" was not found");
                return;
            }
            Debug.Log($"[VehicleColours] Reading {pathToCarColsDat}");

            var lines = File.ReadAllLines(pathToCarColsDat);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                int commentIndex = line.IndexOf('#');
                if (commentIndex >= 0)
                {
                    line = line.Substring(0, commentIndex);
                }

                line = line.Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if(line.Equals(ECarColsSection.Col.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    m_Section = ECarColsSection.Col;
                    continue;
                }                
                if(line.Equals(ECarColsSection.Car.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    m_Section = ECarColsSection.Car;
                    continue;
                }
                if(line.Equals("end", StringComparison.OrdinalIgnoreCase))
                {
                    m_Section = ECarColsSection.None;
                    continue;
                }

                // Example parsed line when section=col: 5,5,5
                // Example parsed line when section=car: bellyup,19,73,31,74,45,75,58,76,64,77,73,76,34,75,24,74
                switch(m_Section)
                {
                    case ECarColsSection.Col:
                        ParseColSection(line, lineIndex + 1);
                        break;
                    case ECarColsSection.Car:
                        ParseCarSection(line, lineIndex + 1);
                        break;
                    default:
                        // ignore
                        break;
                }
            }
        }

        private static void ParseCarSection(string line, int lineNumber)
        {
            // Format: ModelName, FirstColor1, SecondColor1, FirstColor2, SecondColor2, FirstColor3, SecondColor3, FirstColor4, SecondColor4, FirstColor5, SecondColor5, FirstColor6, SecondColor6, FirstColor7, SecondColor7, FirstColor8, SecondColor8
            VehicleSpecificColours specificColours = new();
            specificColours.ColourPairs = new();

            // Parse ModelName first, and then parse every 2 parts afterwards. Vanilla GTA3 assumes 8 pairs but its just as easy for us to support more than 8
            string[] parts = line.Split(',');
            string modelName = parts[0].Trim(); // ModelName
            if(string.IsNullOrEmpty(modelName))
            {
                Debug.LogWarning($"[VehicleColours] Ignoring line {lineNumber} due to empty/null modelName: {line}");
                return;
            }
            if(m_VehicleSpecificColours.ContainsKey(modelName))
            {
                Debug.LogWarning($"[VehicleColours] Ignoring line {lineNumber} due to duplicate enty: {line}");
                return;
            }
            for(int i = 1; i < parts.Length; i += 2)
            {
                VehicleSpecificColours.VehicleColourPair colourPair = new();
                try
                {
                    int col1, col2;
                    col1 = int.Parse(parts[i]);
                    col2 = int.Parse(parts[i+1]);
                    if(!m_VehicleColourDefinitions.ContainsKey(col1))
                    {
                        Debug.LogWarning($"[VehicleColours] Vehicle '{modelName}' references undefined colour {col1} at line {lineNumber}.");
                        col1 = StartingColourId;
                    }
                    if(!m_VehicleColourDefinitions.ContainsKey(col2))
                    {
                        Debug.LogWarning($"[VehicleColours] Vehicle '{modelName}' references undefined colour {col2} at line {lineNumber}.");
                        col2 = StartingColourId;
                    }

                    colourPair.FirstColour = col1;
                    colourPair.SecondColour = col2;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[VehicleColours] Invalid colours at line {lineNumber}, parts {i+1} and {i+2}: {line}\n{exception.Message}");
                    // Revert to StartingColourId on error
                    colourPair.FirstColour = StartingColourId;
                    colourPair.SecondColour = StartingColourId;
                }
                specificColours.ColourPairs.Add(colourPair);
            }
            m_VehicleSpecificColours.Add(modelName, specificColours);
        }

        private static void ParseColSection(string line, int lineNumber)
        {
            // Future (not now) improvement for mods: Provide support for #RRGGBB, different Unity workflow mode and surface types to give mods greater access to unity materials.
            // Current format: Red, Green, Blue
            VehicleColourDefinition colourDefinition = new();
            string[] parts = line.Split(',');
            if(parts.Length != 3)
            {
                Debug.LogWarning($"[VehicleColours] Invalid colour definition at line {lineNumber}: {line}\nExpected exactly 3 parts");
                colourDefinition = GetWhite();
            }
            else
            {
                try
                {
                    colourDefinition.Red = byte.Parse(parts[0]);
                    colourDefinition.Green = byte.Parse(parts[1]);
                    colourDefinition.Blue = byte.Parse(parts[2]);
                }
                catch(FormatException formatException)
                {
                    Debug.LogWarning($"[VehicleColours] Invalid colour definition at line {lineNumber}: {line}\n{formatException.Message}");
                    colourDefinition = GetWhite();
                }
                catch (OverflowException overflowException)
                {
                    Debug.LogWarning($"[VehicleColours] Invalid colour definition at line {lineNumber}: {line}\n{overflowException.Message}");
                    colourDefinition = GetWhite();
                }
            }
            m_VehicleColourDefinitions.Add(m_NextColourId, colourDefinition);
            m_NextColourId++;
        }

        // In cases where a colour is malformed, simply return 255,255,255 (white)
        public static VehicleColourDefinition GetWhite()
        {
            return new VehicleColourDefinition
            {
                Red = 255,
                Green = 255,
                Blue = 255
            };
        }
    }
}
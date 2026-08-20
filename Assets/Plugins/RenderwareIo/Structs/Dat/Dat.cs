using System;
using System.Collections.Generic;
using UnityEngine;
using System.Text;

namespace RenderWareIo.Structs.Dat
{
    [Serializable]
    public struct Dat
    {
        public List<string> Ipls;
        public List<string> Ides;
        public List<string> ModelFiles;
        // public List<GunWeapon> Guns;
        // public List<MeleeWeapon> MeleeWeapons;

        private void ParseLine(string line)
        {
            if (line.TrimStart().StartsWith("#"))
            {
                return;
            }

            line = line.Replace("\t", " ");

            while (line.IndexOf("  ") >= 0)
                line = line.Replace("  ", " ");

            var splits = line.Split(' ');


            switch (splits[0])
            {
                case "IPL":
                    this.Ipls.Add(splits[1]);
                    break;
                case "IDE":
                    this.Ides.Add(splits[1]);
                    break;
                case "MODELFILE":
                    this.ModelFiles.Add(splits[1]);
                    break;
                // case "�":
                //     this.MeleeWeapons.Add(new MeleeWeapon().Read(line));
                //     break;
                // case "$":
                //     this.Guns.Add(new GunWeapon().Read(line));
                //     break;
            }
        }

        public Dat Read(string content)
        {
            this.Ipls = new List<string>();
            this.Ides = new List<string>();
            this.ModelFiles = new List<string>();
            // this.Guns = new List<GunWeapon>();
            // this.MeleeWeapons = new List<MeleeWeapon>();

            string[] lines = content.Split('\n');
            foreach (string line in lines)
            {
                this.ParseLine(line);
            }

            return this;
        }

        public string Write()
        {
            string value = "objs";

            return value;
        }
    }
}

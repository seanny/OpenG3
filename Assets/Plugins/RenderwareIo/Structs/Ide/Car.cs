using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace RenderWareIo.Structs.Ide
{
    [Serializable]
    public struct Car : IIdeEntity<Car>, IModelTxd
    {
        public int Id;
        public string ModelName;
        public string TxdName;
        public string Type;
        public string HandlingId;
        public string GameName;
        public string Anims;
        public string Class;
        public int Frequency;
        public int Level;
        public uint CompRules;
        public int WheelModelId;
        public float WheelScale;
        public int LODModel;

        string IModelTxd.ModelName => ModelName;
        string IModelTxd.TxdName => TxdName;


        public Car Read(string line)
        {
            line = line.Replace(" ", "").Replace("\t", "");

            while (line.IndexOf("\t\t") > 0)
                line = line.Replace("\t\t", "\t");

            string[] splits = line.Split(',');

            this.Id = int.Parse(splits[0]);
            this.ModelName = splits[1];
            this.TxdName = splits[2];
            this.Type = splits[3];
            this.HandlingId = splits[4];
            this.GameName = splits[5];
            this.Class = splits[6];
            this.Frequency = int.Parse(splits[7]);
            this.Level = int.Parse(splits[8]);
            this.CompRules = Convert.ToUInt32(ReplaceInvalidFileNameCharacters(splits[9].Trim(), ""), 16);
            if(Type == "car")
            {
                this.WheelModelId = int.Parse(splits[10]);
                this.WheelScale = float.Parse(splits[11]);
            }
            else if(Type == "plane")
            {
                this.LODModel = int.Parse(splits[10]);
            }
            return this;
        }

        private string ReplaceInvalidFileNameCharacters(string fileName, string replacement)
        {
            string regexSearch = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            Regex r = new Regex(string.Format("[{0}]", Regex.Escape(regexSearch)));
            return r.Replace(fileName, replacement);
        }

        public string Write()
        {
            throw new NotImplementedException();
        }
    }
}

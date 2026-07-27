using RenderWareIo.Structs.Dff;
using RenderWareIo.Structs.Dff.Plugins;
using RenderWareIo.Structs.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace RenderWareIo
{
    public class DffFile
    {
        Stream stream;

        public Dff Dff { get; set; }


        public DffFile()
        {
            this.stream = new MemoryStream();

            this.Dff = new Dff();
        }

        public DffFile(Dff dff)
        {
            this.Dff = dff;
        }

        public DffFile(byte[] data)
        {
            this.stream = new MemoryStream(data);

            this.Dff = (new Dff()).Read(this.stream);
        }

        public DffFile(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"Dff file '{path}' does not exist");
                return;
            }

            Debug.Log($"Loading model {path}");
            this.stream = File.Open(path, FileMode.Open);

            this.Dff = (new Dff()).Read(this.stream);
            this.stream.Close();
        }

        public bool TryGetEmbeddedDff(string modelName, out DffFile embeddedFile)
        {
            embeddedFile = null;

            if (string.IsNullOrWhiteSpace(modelName) ||
                this.Dff?.Clump?.FrameList?.Frames == null ||
                this.Dff.Clump.GeometryList?.Geometries == null ||
                this.Dff.Clump.Atomics == null)
            {
                return false;
            }

            string targetName = Path.GetFileNameWithoutExtension(modelName);
            FrameList frameList = this.Dff.Clump.FrameList;
            int rootFrameIndex = FindFrame(frameList, targetName);

            if (rootFrameIndex < 0)
            {
                return false;
            }

            HashSet<uint> frameIndices = new() { (uint)rootFrameIndex };
            bool addedFrame;

            do
            {
                addedFrame = false;

                for (int frameIndex = 0; frameIndex < frameList.Frames.Count; frameIndex++)
                {
                    if (frameIndices.Contains((uint)frameIndex) ||
                        !frameIndices.Contains(frameList.Frames[frameIndex].Parent))
                    {
                        continue;
                    }

                    frameIndices.Add((uint)frameIndex);
                    addedFrame = true;
                }
            }
            while (addedFrame);

            List<int> sourceGeometryIndices = new();
            foreach (Atomic atomic in this.Dff.Clump.Atomics)
            {
                if (!frameIndices.Contains(atomic.FrameIndex) ||
                    atomic.GeometryIndex >= this.Dff.Clump.GeometryList.Geometries.Count)
                {
                    continue;
                }

                int geometryIndex = (int)atomic.GeometryIndex;
                if (!sourceGeometryIndices.Contains(geometryIndex))
                {
                    sourceGeometryIndices.Add(geometryIndex);
                }
            }

            if (sourceGeometryIndices.Count == 0)
            {
                return false;
            }

            sourceGeometryIndices.Sort();
            Dictionary<int, uint> geometryIndices = new();
            List<Geometry> geometries = new(sourceGeometryIndices.Count);

            for (int geometryIndex = 0; geometryIndex < sourceGeometryIndices.Count; geometryIndex++)
            {
                int sourceIndex = sourceGeometryIndices[geometryIndex];
                geometryIndices[sourceIndex] = (uint)geometryIndex;
                geometries.Add(this.Dff.Clump.GeometryList.Geometries[sourceIndex]);
            }

            List<Atomic> atomics = new();
            foreach (Atomic sourceAtomic in this.Dff.Clump.Atomics)
            {
                if (!frameIndices.Contains(sourceAtomic.FrameIndex) ||
                    !geometryIndices.TryGetValue((int)sourceAtomic.GeometryIndex, out uint geometryIndex))
                {
                    continue;
                }

                atomics.Add(new Atomic
                {
                    Header = CloneHeader(sourceAtomic.Header),
                    StructHeader = CloneHeader(sourceAtomic.StructHeader),
                    FrameIndex = sourceAtomic.FrameIndex,
                    GeometryIndex = geometryIndex,
                    Flags = sourceAtomic.Flags,
                    Unused = sourceAtomic.Unused,
                    Extension = sourceAtomic.Extension,
                });
            }

            Clump sourceClump = this.Dff.Clump;
            Clump embeddedClump = new()
            {
                Header = CloneHeader(sourceClump.Header),
                AtomicCount = atomics.Count,
                LightCount = sourceClump.LightCount,
                CameraCount = sourceClump.CameraCount,
                FrameList = sourceClump.FrameList,
                GeometryList = new GeometryList
                {
                    Header = CloneHeader(sourceClump.GeometryList.Header),
                    StructHeader = CloneHeader(sourceClump.GeometryList.StructHeader),
                    GeometryCount = geometries.Count,
                    Geometries = geometries,
                },
                Atomics = atomics,
                Extension = sourceClump.Extension,
            };

            embeddedFile = new DffFile(new Dff
            {
                Header = CloneHeader(this.Dff.Header),
                Clump = embeddedClump,
            });
            Debug.Log($"Loaded embedded dff {targetName}");
            return true;
        }

        private static int FindFrame(FrameList frameList, string targetName)
        {
            if (string.IsNullOrEmpty(targetName) || frameList.Extensions == null)
            {
                return -1;
            }

            for (int frameIndex = 0;
                 frameIndex < frameList.Extensions.Count && frameIndex < frameList.Frames.Count;
                 frameIndex++)
            {
                Extension extension = frameList.Extensions[frameIndex];
                if (extension?.Extensions == null)
                {
                    continue;
                }

                foreach (IExtensionPlugin plugin in extension.Extensions)
                {
                    if (plugin is FramePlugin framePlugin &&
                        string.Equals(
                            CleanFrameName(framePlugin.Value),
                            targetName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return frameIndex;
                    }
                }
            }

            return -1;
        }

        private static string CleanFrameName(string frameName)
        {
            return frameName?.TrimEnd('\0', ' ', '\r', '\n', '\t');
        }

        private static ChunkHeader CloneHeader(ChunkHeader header)
        {
            return header == null
                ? null
                : new ChunkHeader
                {
                    Type = header.Type,
                    Size = header.Size,
                    Marker = header.Marker,
                };
        }

        public void Write(string path)
        {
            using (Stream stream = new MemoryStream())
            {
                this.Dff.Write(stream);

                byte[] buffer = new byte[stream.Length];
                stream.Position = 0;
                int bytesRead = stream.Read(buffer, 0, (int)stream.Length);

                File.WriteAllBytes(path, buffer);
            }

        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using RenderWareIo;
using RenderWareIo.Structs.Dff;
using RenderWareIo.Structs.Ide;
using RenderWareIo.Structs.Txd;
using UnityEngine;
using UnityEngine.Rendering;
using DffMaterial = RenderWareIo.Structs.Dff.Material;
using ImgFile = GTA3Unity.Img.ImgFile;
using TxdTexture = RenderWareIo.Structs.Txd.Texture;

namespace GTA3Unity.Utility
{
    public sealed class TxdMaterialCache
    {
        private const string GenericTxdName = "generic";

        public IReadOnlyDictionary<string, Texture2D> Textures => m_Textures;

        private ImgFile m_ImgFile;
        private UnityEngine.Material m_FallbackMaterial;
        private readonly Dictionary<string, TxdFile> m_TxdFiles = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TxdTexture> m_TxdTextures = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Texture2D> m_Textures = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, UnityEngine.Material> m_Materials = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> m_TxdParents = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> m_TxdChildren = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> m_LooseTxdDirectories = new();
        private readonly HashSet<string> m_Warnings = new(StringComparer.OrdinalIgnoreCase);

        private int m_TxdCacheHits;
        private int m_TxdCacheMisses;
        private int m_DecodedTextureCount;
        private int m_FallbackSlotCount;

        public void SetImageFile(ImgFile imgFile, UnityEngine.Material fallbackMaterial)
        {
            m_ImgFile = imgFile ?? throw new ArgumentNullException(nameof(imgFile));
            m_FallbackMaterial = fallbackMaterial;
        }

        public void RegisterLooseTxdDirectory(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return;
            }

            string normalizedPath = Path.GetFullPath(directoryPath);

            if (!Directory.Exists(normalizedPath))
            {
                WarnOnce(
                    $"missing-loose-txd-directory:{normalizedPath}",
                    $"Could not find loose TXD directory '{normalizedPath}'.");
                return;
            }

            if (m_LooseTxdDirectories.Exists(path => string.Equals(
                    path,
                    normalizedPath,
                    StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            m_LooseTxdDirectories.Add(normalizedPath);
        }

        public bool RegisterTxdFile(string txdName, string path)
        {
            string txdKey = NormalizeName(txdName);

            if (string.IsNullOrEmpty(txdKey) ||
                string.IsNullOrEmpty(path))
            {
                return false;
            }

            if (!File.Exists(path))
            {
                WarnOnce(
                    $"missing-loose-txd:{txdKey}",
                    $"Could not find loose TXD '{path}'.");
                return false;
            }

            try
            {
                m_TxdFiles[txdKey] = new TxdFile(path);
                Debug.Log($"Loaded loose TXD '{txdKey}' from '{path}'.");
                return true;
            }
            catch (Exception exception)
            {
                WarnOnce(
                    $"bad-loose-txd:{txdKey}",
                    $"Failed to load loose TXD '{path}': {exception.Message}");
                return false;
            }
        }

        public void RegisterTxdParents(IEnumerable<Txdp> txdParents)
        {
            if (txdParents == null)
            {
                return;
            }

            foreach (Txdp txdParent in txdParents)
            {
                string sourceTxd = NormalizeName(txdParent.SourceTxd);
                string targetTxd = NormalizeName(txdParent.TargetTxd);

                if (string.IsNullOrEmpty(sourceTxd) ||
                    string.IsNullOrEmpty(targetTxd) ||
                    string.Equals(sourceTxd, targetTxd, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                m_TxdParents[sourceTxd] = targetTxd;

                if (!m_TxdChildren.TryGetValue(targetTxd, out List<string> children))
                {
                    children = new List<string>();
                    m_TxdChildren[targetTxd] = children;
                }

                if (!children.Exists(child => string.Equals(
                        child,
                        sourceTxd,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    children.Add(sourceTxd);
                }
            }
        }

        public UnityEngine.Material[] CreateMaterials(string txdName, Geometry geometry)
        {
            int materialCount = GetMaterialCount(geometry);
            UnityEngine.Material[] materials = new UnityEngine.Material[materialCount];

            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = CreateMaterial(txdName, geometry, i);
            }

            return materials;
        }

        public void LogStats()
        {
            Debug.Log(
                $"TXD material cache: txd hits={m_TxdCacheHits}, " +
                $"txd misses={m_TxdCacheMisses}, decoded textures={m_DecodedTextureCount}, " +
                $"fallback slots={m_FallbackSlotCount}.");
        }

        private UnityEngine.Material CreateMaterial(string txdName, Geometry geometry, int materialIndex)
        {
            DffMaterial dffMaterial = GetDffMaterial(geometry, materialIndex);

            if (dffMaterial == null ||
                dffMaterial.Textured <= 0 ||
                dffMaterial.Texture?.Name == null)
            {
                return Fallback($"untextured:{NormalizeName(txdName)}:{materialIndex}");
            }

            string textureName = NormalizeName(dffMaterial.Texture.Name.Value);

            if (string.IsNullOrEmpty(textureName))
            {
                return Fallback($"empty-texture-name:{NormalizeName(txdName)}:{materialIndex}");
            }

            Texture2D texture = LoadTexture(txdName, textureName, out string resolvedTxdName);

            if (texture == null)
            {
                return Fallback($"missing-texture:{NormalizeName(txdName)}:{textureName}");
            }

            string materialKey = $"{resolvedTxdName}/{textureName}";

            if (m_Materials.TryGetValue(materialKey, out UnityEngine.Material cachedMaterial))
            {
                return cachedMaterial;
            }

            UnityEngine.Material material = m_FallbackMaterial != null
                ? new UnityEngine.Material(m_FallbackMaterial)
                : new UnityEngine.Material(Shader.Find("Universal Render Pipeline/Lit"));

            material.name = materialKey;

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }

            material.SetFloat("_Cull", (float)CullMode.Off);
            // if (material.HasProperty("_Cull"))
            // {
            //     material.SetFloat("_Cull", (float)CullMode.Off);
            // }

            m_Materials[materialKey] = material;
            return material;
        }

        public Texture2D LoadTexture(
            string txdName,
            string textureName,
            out string resolvedTxdName)
        {
            string txdKey = NormalizeName(txdName);
            resolvedTxdName = txdKey;

            if (string.IsNullOrEmpty(txdKey))
            {
                WarnOnce("empty-txd", "Cannot resolve texture because the TXD name is empty.");
                return null;
            }

            string textureKey = $"{txdKey}/{textureName}";

            if (m_Textures.TryGetValue(textureKey, out Texture2D cachedTexture))
            {
                return cachedTexture;
            }

            TxdTexture txdTexture = FindTxdTexture(
                txdKey,
                textureName,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                out resolvedTxdName);

            if (txdTexture?.Data == null)
            {
                return null;
            }

            textureKey = $"{resolvedTxdName}/{textureName}";

            if (m_Textures.TryGetValue(textureKey, out cachedTexture))
            {
                return cachedTexture;
            }

            Texture2D texture = DecodeTexture(textureKey, txdTexture.Data);

            if (texture == null)
            {
                return null;
            }

            m_Textures[textureKey] = texture;
            m_DecodedTextureCount++;
            return texture;
        }

        private TxdTexture FindTxdTexture(
            string txdName,
            string textureName,
            HashSet<string> visitedTxdNames,
            out string resolvedTxdName)
        {
            resolvedTxdName = txdName;

            if (!visitedTxdNames.Add(txdName))
            {
                WarnOnce(
                    $"txd-parent-cycle:{txdName}",
                    $"Detected a TXD parent cycle while resolving '{textureName}' from '{txdName}'.");
                return null;
            }

            string textureKey = $"{txdName}/{textureName}";

            if (m_TxdTextures.TryGetValue(textureKey, out TxdTexture cachedTexture))
            {
                return cachedTexture;
            }

            TxdFile txdFile = LoadTxd(txdName);

            if (txdFile?.Txd?.TextureContainer?.Textures == null)
            {
                return null;
            }

            foreach (TxdTexture texture in txdFile.Txd.TextureContainer.Textures)
            {
                string candidateName = NormalizeName(texture.Data?.TextureName);

                if (string.IsNullOrEmpty(candidateName))
                {
                    continue;
                }

                string candidateKey = $"{txdName}/{candidateName}";

                if (!m_TxdTextures.ContainsKey(candidateKey))
                {
                    m_TxdTextures[candidateKey] = texture;
                }
            }

            if (m_TxdTextures.TryGetValue(textureKey, out cachedTexture))
            {
                return cachedTexture;
            }

            if (m_TxdParents.TryGetValue(txdName, out string parentTxdName))
            {
                TxdTexture parentTexture = FindTxdTexture(
                    parentTxdName,
                    textureName,
                    visitedTxdNames,
                    out resolvedTxdName);

                if (parentTexture != null)
                {
                    return parentTexture;
                }
            }

            if (m_TxdChildren.TryGetValue(txdName, out List<string> childTxdNames))
            {
                foreach (string childTxdName in childTxdNames)
                {
                    TxdTexture childTexture = FindTxdTexture(
                        childTxdName,
                        textureName,
                        visitedTxdNames,
                        out resolvedTxdName);

                    if (childTexture != null)
                    {
                        return childTexture;
                    }
                }
            }

            if (!string.Equals(txdName, GenericTxdName, StringComparison.OrdinalIgnoreCase))
            {
                TxdTexture genericTexture = FindTxdTextureInSingleDictionary(
                    GenericTxdName,
                    textureName);

                if (genericTexture != null)
                {
                    resolvedTxdName = GenericTxdName;
                    return genericTexture;
                }
            }

            WarnOnce(
                $"missing-texture:{textureKey}",
                $"TXD '{txdName}' and related TXD dictionaries do not contain texture '{textureName}'.");

            return null;
        }

        private TxdTexture FindTxdTextureInSingleDictionary(
            string txdName,
            string textureName)
        {
            string textureKey = $"{txdName}/{textureName}";

            if (m_TxdTextures.TryGetValue(textureKey, out TxdTexture cachedTexture))
            {
                return cachedTexture;
            }

            TxdFile txdFile = LoadTxd(txdName);

            if (txdFile?.Txd?.TextureContainer?.Textures == null)
            {
                return null;
            }

            foreach (TxdTexture texture in txdFile.Txd.TextureContainer.Textures)
            {
                string candidateName = NormalizeName(texture.Data?.TextureName);

                if (string.IsNullOrEmpty(candidateName))
                {
                    continue;
                }

                string candidateKey = $"{txdName}/{candidateName}";

                if (!m_TxdTextures.ContainsKey(candidateKey))
                {
                    m_TxdTextures[candidateKey] = texture;
                }
            }

            return m_TxdTextures.TryGetValue(textureKey, out cachedTexture)
                ? cachedTexture
                : null;
        }

        private TxdFile LoadTxd(string txdName)
        {
            if (m_TxdFiles.TryGetValue(txdName, out TxdFile cachedTxd))
            {
                m_TxdCacheHits++;
                return cachedTxd;
            }

            string fileName = $"{txdName}.txd";

            if (m_ImgFile.Contains(fileName))
            {
                try
                {
                    TxdFile txdFile = new TxdFile(m_ImgFile[fileName].GetData());
                    m_TxdFiles[txdName] = txdFile;
                    return txdFile;
                }
                catch (Exception exception)
                {
                    WarnOnce(
                        $"bad-txd:{txdName}",
                        $"Failed to load TXD '{fileName}': {exception.Message}");
                    return null;
                }
            }

            if (TryLoadLooseTxd(txdName, out TxdFile looseTxdFile))
            {
                return looseTxdFile;
            }

            m_TxdCacheMisses++;
            WarnOnce(
                $"missing-txd:{txdName}",
                $"Could not find TXD '{fileName}' in gta3.img or registered loose TXD directories.");
            return null;
        }

        private bool TryLoadLooseTxd(string txdName, out TxdFile txdFile)
        {
            txdFile = null;

            foreach (string directoryPath in m_LooseTxdDirectories)
            {
                string path = Path.Combine(directoryPath, $"{txdName}.txd");

                if (!File.Exists(path))
                {
                    continue;
                }

                try
                {
                    txdFile = new TxdFile(path);
                    m_TxdFiles[txdName] = txdFile;
                    return true;
                }
                catch (Exception exception)
                {
                    WarnOnce(
                        $"bad-loose-txd:{txdName}:{path}",
                        $"Failed to load loose TXD '{path}': {exception.Message}");
                    return false;
                }
            }

            return false;
        }

        private Texture2D DecodeTexture(string textureKey, TextureData data)
        {
            if (data.Data == null ||
                data.Data.Length == 0 ||
                data.Width == 0 ||
                data.Height == 0)
            {
                WarnOnce(
                    $"empty-texture:{textureKey}",
                    $"Texture '{textureKey}' has no image data.");
                return null;
            }

            // FIXME: Texture 'particle/explo01' has unsupported format 'UNKNOWN' depth 8, data size 252645135
            try
            {
                if (TryDecodePalettedTexture(textureKey, data, out Texture2D palettedTexture))
                {
                    return palettedTexture;
                }

                if (TryDecodeRawTexture(textureKey, data, out Texture2D rawTexture))
                {
                    return rawTexture;
                }

                if (TryDecodeDxt1Texture(textureKey, data, out Texture2D dxt1Texture))
                {
                    return dxt1Texture;
                }

                if (TryDecodeDxt3Texture(textureKey, data, out Texture2D dxt3Texture))
                {
                    return dxt3Texture;
                }

                if (TryDecodeDxt5Texture(textureKey, data, out Texture2D dxt5Texture))
                {
                    return dxt5Texture;
                }

                WarnOnce(
                    $"unsupported-format:{textureKey}:{data.TextureFormat}:{data.Depth}",
                    $"Texture '{textureKey}' has unsupported format '{data.TextureFormatString}' " +
                    $"depth {data.Depth}, data size {data.Data.Length}.");
                return null;
            }
            catch (Exception exception)
            {
                WarnOnce(
                    $"decode-failed:{textureKey}",
                    $"Failed to decode texture '{textureKey}': {exception.Message}");
                return null;
            }
        }

        private bool TryDecodePalettedTexture(
            string textureKey,
            TextureData data,
            out Texture2D texture)
        {
            texture = null;

            int pixelCount = data.Width * data.Height;

            if (data.Pallette == null)
            {
                return false;
            }

            if (data.Depth == 8)
            {
                if (data.Pallette.Length < 256 * 4 ||
                    data.Data.Length != pixelCount)
                {
                    return false;
                }

                Color32[] colors = new Color32[pixelCount];

                for (int y = 0; y < data.Height; y++)
                {
                    for (int x = 0; x < data.Width; x++)
                    {
                        int sourceIndex = x + (data.Width * y);
                        int targetIndex = x + (data.Width * (data.Height - y - 1));

                        colors[targetIndex] = ReadPaletteColor(data.Pallette, data.Data[sourceIndex]);
                    }
                }

                texture = CreateTexture(textureKey, data.Width, data.Height, colors);
                return true;
            }

            if (data.Depth == 4)
            {
                int expectedDataLength = (pixelCount + 1) / 2;

                if (data.Pallette.Length < 16 * 4 ||
                    data.Data.Length != expectedDataLength)
                {
                    return false;
                }

                Color32[] colors = new Color32[pixelCount];

                for (int y = 0; y < data.Height; y++)
                {
                    for (int x = 0; x < data.Width; x++)
                    {
                        int sourceIndex = x + (data.Width * y);
                        int byteIndex = sourceIndex / 2;
                        byte packedIndices = data.Data[byteIndex];
                        int paletteIndex = (sourceIndex & 1) == 0
                            ? packedIndices & 0x0F
                            : packedIndices >> 4;
                        int targetIndex = x + (data.Width * (data.Height - y - 1));

                        colors[targetIndex] = ReadPaletteColor(data.Pallette, paletteIndex);
                    }
                }

                texture = CreateTexture(textureKey, data.Width, data.Height, colors);
                return true;
            }

            return false;
        }

        private static Color32 ReadPaletteColor(
            byte[] palette,
            int colorIndex)
        {
            int paletteIndex = colorIndex * 4;

            return new Color32(
                palette[paletteIndex],
                palette[paletteIndex + 1],
                palette[paletteIndex + 2],
                palette[paletteIndex + 3]);
        }

        private static bool TryDecodeRawTexture(
            string textureKey,
            TextureData data,
            out Texture2D texture)
        {
            texture = null;

            int pixelCount = data.Width * data.Height;

            if (data.Depth == 32 &&
                data.Data.Length == pixelCount * 4)
            {
                Color32[] colors = new Color32[pixelCount];

                for (int y = 0; y < data.Height; y++)
                {
                    for (int x = 0; x < data.Width; x++)
                    {
                        int sourceIndex = x + (data.Width * y);
                        int dataIndex = sourceIndex * 4;
                        int targetIndex = x + (data.Width * (data.Height - y - 1));

                        colors[targetIndex] = data.TextureFormat == 32 || data.TextureFormat == 33
                            ? new Color32(
                                data.Data[dataIndex],
                                data.Data[dataIndex + 1],
                                data.Data[dataIndex + 2],
                                data.Data[dataIndex + 3])
                            : new Color32(
                                data.Data[dataIndex + 2],
                                data.Data[dataIndex + 1],
                                data.Data[dataIndex],
                                data.Data[dataIndex + 3]);
                    }
                }

                texture = CreateTexture(textureKey, data.Width, data.Height, colors);
                return true;
            }

            if (data.Depth == 24 &&
                data.Data.Length == pixelCount * 3)
            {
                Color32[] colors = new Color32[pixelCount];

                for (int y = 0; y < data.Height; y++)
                {
                    for (int x = 0; x < data.Width; x++)
                    {
                        int sourceIndex = x + (data.Width * y);
                        int dataIndex = sourceIndex * 3;
                        int targetIndex = x + (data.Width * (data.Height - y - 1));

                        colors[targetIndex] = new Color32(
                            data.Data[dataIndex + 2],
                            data.Data[dataIndex + 1],
                            data.Data[dataIndex],
                            255);
                    }
                }

                texture = CreateTexture(textureKey, data.Width, data.Height, colors);
                return true;
            }

            if (data.Depth == 16 &&
                data.Data.Length == pixelCount * 2)
            {
                Color32[] colors = new Color32[pixelCount];

                for (int y = 0; y < data.Height; y++)
                {
                    for (int x = 0; x < data.Width; x++)
                    {
                        int sourceIndex = x + (data.Width * y);
                        int dataIndex = sourceIndex * 2;
                        ushort value = BitConverter.ToUInt16(data.Data, dataIndex);
                        int targetIndex = x + (data.Width * (data.Height - y - 1));

                        colors[targetIndex] = Decode16BitColor(value, data.TextureFormat);
                    }
                }

                texture = CreateTexture(textureKey, data.Width, data.Height, colors);
                return true;
            }

            return false;
        }

        private static bool TryDecodeDxt1Texture(
            string textureKey,
            TextureData data,
            out Texture2D texture)
        {
            texture = null;

            if (!IsDxt1(data) ||
                data.Data.Length != GetDxt1Size(data.Width, data.Height))
            {
                return false;
            }

            Color32[] colors = new Color32[data.Width * data.Height];
            int offset = 0;

            for (int y = 0; y < data.Height; y += 4)
            {
                for (int x = 0; x < data.Width; x += 4)
                {
                    ushort color0 = BitConverter.ToUInt16(data.Data, offset);
                    ushort color1 = BitConverter.ToUInt16(data.Data, offset + 2);
                    uint indices = BitConverter.ToUInt32(data.Data, offset + 4);
                    offset += 8;

                    Color32[] blockColors = CreateDxtColorTable(color0, color1, data.Depth == 16);

                    for (int yy = 0; yy < 4; yy++)
                    {
                        for (int xx = 0; xx < 4; xx++)
                        {
                            int targetX = x + xx;
                            int targetY = data.Height - y - yy - 1;

                            if (targetX < data.Width && targetY >= 0)
                            {
                                int colorIndex = (int)(indices & 0x03);
                                colors[targetX + (data.Width * targetY)] = blockColors[colorIndex];
                            }

                            indices >>= 2;
                        }
                    }
                }
            }

            texture = CreateTexture(textureKey, data.Width, data.Height, colors);
            return true;
        }

        private static bool TryDecodeDxt3Texture(
            string textureKey,
            TextureData data,
            out Texture2D texture)
        {
            texture = null;

            if (!IsDxt3(data.TextureFormat) ||
                data.Data.Length != GetDxt5Size(data.Width, data.Height))
            {
                return false;
            }

            Color32[] colors = new Color32[data.Width * data.Height];
            int offset = 0;

            for (int y = 0; y < data.Height; y += 4)
            {
                for (int x = 0; x < data.Width; x += 4)
                {
                    ulong alphaBlock = BitConverter.ToUInt64(data.Data, offset);
                    ushort color0 = BitConverter.ToUInt16(data.Data, offset + 8);
                    ushort color1 = BitConverter.ToUInt16(data.Data, offset + 10);
                    uint indices = BitConverter.ToUInt32(data.Data, offset + 12);
                    offset += 16;

                    Color32[] blockColors = CreateDxtColorTable(color0, color1, hasDxt1Alpha: false);

                    for (int yy = 0; yy < 4; yy++)
                    {
                        for (int xx = 0; xx < 4; xx++)
                        {
                            int targetX = x + xx;
                            int targetY = data.Height - y - yy - 1;

                            if (targetX < data.Width && targetY >= 0)
                            {
                                int colorIndex = (int)(indices & 0x03);
                                Color32 color = blockColors[colorIndex];
                                color.a = (byte)((alphaBlock & 0x0F) * 17);
                                colors[targetX + (data.Width * targetY)] = color;
                            }

                            indices >>= 2;
                            alphaBlock >>= 4;
                        }
                    }
                }
            }

            texture = CreateTexture(textureKey, data.Width, data.Height, colors);
            return true;
        }

        private static bool TryDecodeDxt5Texture(
            string textureKey,
            TextureData data,
            out Texture2D texture)
        {
            texture = null;

            if (!IsDxt5(data.TextureFormat) ||
                data.Data.Length != GetDxt5Size(data.Width, data.Height))
            {
                return false;
            }

            Color32[] colors = new Color32[data.Width * data.Height];
            int offset = 0;

            for (int y = 0; y < data.Height; y += 4)
            {
                for (int x = 0; x < data.Width; x += 4)
                {
                    byte alpha0 = data.Data[offset];
                    byte alpha1 = data.Data[offset + 1];
                    ulong alphaIndices = 0;

                    for (int i = 0; i < 6; i++)
                    {
                        alphaIndices |= (ulong)data.Data[offset + 2 + i] << (8 * i);
                    }

                    ushort color0 = BitConverter.ToUInt16(data.Data, offset + 8);
                    ushort color1 = BitConverter.ToUInt16(data.Data, offset + 10);
                    uint colorIndices = BitConverter.ToUInt32(data.Data, offset + 12);
                    offset += 16;

                    Color32[] blockColors = CreateDxtColorTable(color0, color1, hasDxt1Alpha: false);
                    byte[] blockAlphas = CreateDxt5AlphaTable(alpha0, alpha1);

                    for (int yy = 0; yy < 4; yy++)
                    {
                        for (int xx = 0; xx < 4; xx++)
                        {
                            int targetX = x + xx;
                            int targetY = data.Height - y - yy - 1;

                            if (targetX < data.Width && targetY >= 0)
                            {
                                int colorIndex = (int)(colorIndices & 0x03);
                                int alphaIndex = (int)(alphaIndices & 0x07);
                                Color32 color = blockColors[colorIndex];
                                color.a = blockAlphas[alphaIndex];
                                colors[targetX + (data.Width * targetY)] = color;
                            }

                            colorIndices >>= 2;
                            alphaIndices >>= 3;
                        }
                    }
                }
            }

            texture = CreateTexture(textureKey, data.Width, data.Height, colors);
            return true;
        }

        private static Color32[] CreateDxtColorTable(
            ushort color0,
            ushort color1,
            bool hasDxt1Alpha)
        {
            Color32[] colors = new Color32[4];
            colors[0] = DecodeRgb565(color0);
            colors[1] = DecodeRgb565(color1);

            if (color0 > color1 || !hasDxt1Alpha)
            {
                colors[2] = LerpColor(colors[0], colors[1], 2, 1);
                colors[3] = LerpColor(colors[0], colors[1], 1, 2);
            }
            else
            {
                colors[2] = LerpColor(colors[0], colors[1], 1, 1);
                colors[3] = new Color32(0, 0, 0, 0);
            }

            return colors;
        }

        private static Color32 LerpColor(
            Color32 color0,
            Color32 color1,
            int weight0,
            int weight1)
        {
            int total = weight0 + weight1;

            return new Color32(
                (byte)((color0.r * weight0 + color1.r * weight1) / total),
                (byte)((color0.g * weight0 + color1.g * weight1) / total),
                (byte)((color0.b * weight0 + color1.b * weight1) / total),
                (byte)((color0.a * weight0 + color1.a * weight1) / total));
        }

        private static byte[] CreateDxt5AlphaTable(byte alpha0, byte alpha1)
        {
            byte[] alphas = new byte[8];
            alphas[0] = alpha0;
            alphas[1] = alpha1;

            if (alpha0 > alpha1)
            {
                alphas[2] = LerpByte(alpha0, alpha1, 6, 1, 7);
                alphas[3] = LerpByte(alpha0, alpha1, 5, 2, 7);
                alphas[4] = LerpByte(alpha0, alpha1, 4, 3, 7);
                alphas[5] = LerpByte(alpha0, alpha1, 3, 4, 7);
                alphas[6] = LerpByte(alpha0, alpha1, 2, 5, 7);
                alphas[7] = LerpByte(alpha0, alpha1, 1, 6, 7);
            }
            else
            {
                alphas[2] = LerpByte(alpha0, alpha1, 4, 1, 5);
                alphas[3] = LerpByte(alpha0, alpha1, 3, 2, 5);
                alphas[4] = LerpByte(alpha0, alpha1, 2, 3, 5);
                alphas[5] = LerpByte(alpha0, alpha1, 1, 4, 5);
                alphas[6] = 0;
                alphas[7] = 255;
            }

            return alphas;
        }

        private static byte LerpByte(
            byte value0,
            byte value1,
            int weight0,
            int weight1,
            int divisor)
        {
            return (byte)((value0 * weight0 + value1 * weight1) / divisor);
        }

        private static Color32 DecodeRgb565(ushort value)
        {
            return new Color32(
                Expand5((value >> 11) & 0x1F),
                Expand6((value >> 5) & 0x3F),
                Expand5(value & 0x1F),
                255);
        }

        private static Color32 Decode16BitColor(ushort value, uint textureFormat)
        {
            if (textureFormat == 26)
            {
                return new Color32(
                    Expand4((value >> 8) & 0x0F),
                    Expand4((value >> 4) & 0x0F),
                    Expand4(value & 0x0F),
                    Expand4((value >> 12) & 0x0F));
            }

            if (textureFormat == 24 ||
                textureFormat == 25)
            {
                return new Color32(
                    Expand5((value >> 10) & 0x1F),
                    Expand5((value >> 5) & 0x1F),
                    Expand5(value & 0x1F),
                    textureFormat == 25 && (value & 0x8000) == 0
                        ? (byte)0
                        : (byte)255);
            }

            return DecodeRgb565(value);
        }

        private static Texture2D CreateTexture(
            string textureKey,
            int width,
            int height,
            Color32[] colors)
        {
            Texture2D texture = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                mipChain: false);

            texture.name = Path.GetFileName(textureKey);
            texture.SetPixels32(colors);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            return texture;
        }

        private static byte Expand4(int value)
        {
            return (byte)((value << 4) | value);
        }

        private static byte Expand5(int value)
        {
            return (byte)((value << 3) | (value >> 2));
        }

        private static byte Expand6(int value)
        {
            return (byte)((value << 2) | (value >> 4));
        }

        private static bool IsDxt1(TextureData data)
        {
            return data.TextureFormat == 0 ||
                data.TextureFormat == FourCc("DXT1");
        }

        private static bool IsDxt3(uint textureFormat)
        {
            return textureFormat == FourCc("DXT2") ||
                textureFormat == FourCc("DXT3");
        }

        private static bool IsDxt5(uint textureFormat)
        {
            return textureFormat == FourCc("DXT4") ||
                textureFormat == FourCc("DXT5");
        }

        private static int GetMaterialCount(Geometry geometry)
        {
            int materialListCount = geometry?.MaterialList?.Materials != null
                ? geometry.MaterialList.Materials.Count
                : 0;

            int referencedMaterialCount = 0;

            if (geometry?.Triangles != null)
            {
                foreach (RenderWareIo.Structs.Dff.Triangle triangle in geometry.Triangles)
                {
                    referencedMaterialCount = Math.Max(
                        referencedMaterialCount,
                        triangle.MaterialIndex + 1);
                }
            }

            int materialCount = Math.Max(materialListCount, referencedMaterialCount);

            if (materialCount == 0)
            {
                return 1;
            }

            return materialCount;
        }

        private static DffMaterial GetDffMaterial(Geometry geometry, int materialIndex)
        {
            if (geometry?.MaterialList?.Materials == null ||
                materialIndex < 0 ||
                materialIndex >= geometry.MaterialList.Materials.Count)
            {
                return null;
            }

            return geometry.MaterialList.Materials[materialIndex];
        }

        private UnityEngine.Material Fallback(string reason)
        {
            m_FallbackSlotCount++;
            return m_FallbackMaterial;
        }

        private void WarnOnce(string key, string message)
        {
            if (m_Warnings.Add(key))
            {
                Debug.LogWarning(message);
            }
        }

        private static string NormalizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string normalized = value.Replace("\0", string.Empty).Trim();
            normalized = Path.GetFileNameWithoutExtension(normalized);
            return normalized.ToLowerInvariant();
        }

        private static uint FourCc(string value)
        {
            byte[] bytes = System.Text.Encoding.ASCII.GetBytes(value);
            return BitConverter.ToUInt32(bytes, 0);
        }

        private static int GetDxt1Size(int width, int height)
        {
            int blockWidth = Math.Max(1, (width + 3) / 4);
            int blockHeight = Math.Max(1, (height + 3) / 4);
            return blockWidth * blockHeight * 8;
        }

        private static int GetDxt5Size(int width, int height)
        {
            int blockWidth = Math.Max(1, (width + 3) / 4);
            int blockHeight = Math.Max(1, (height + 3) / 4);
            return blockWidth * blockHeight * 16;
        }
    }
}

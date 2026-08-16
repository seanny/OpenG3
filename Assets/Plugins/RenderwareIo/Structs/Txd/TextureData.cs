using RenderWareIo.ReadWriteHelpers;
using RenderWareIo.Structs.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RenderWareIo.Structs.Txd
{
    public class TextureDataLookup
    {
        public static Dictionary<uint, string> TextureFormats = new Dictionary<uint, string>()
        {
            [0] = "UNKNOWN",
            [20] = "D3DFMT_R8G8B8",
            [21] = "D3DFMT_A8R8G8B8",
            [22] = "D3DFMT_X8R8G8B8",
            [23] = "D3DFMT_R5G6B5",
            [24] = "D3DFMT_X1R5G5B5",
            [25] = "D3DFMT_A1R5G5B5",
            [26] = "D3DFMT_A4R4G4B4",
            [32] = "D3DFMT_A8B8G8R8",
            [33] = "D3DFMT_X8B8G8R8",
            [40] = "D3DFMT_A8P8",
            [41] = "D3DFMT_P8",
        };
    }

    public class TextureData : IBinaryStructure<TextureData>
    {
        public ChunkHeader Header { get; set; }
        public uint Version { get; set; }
        public uint FilterFlags { get; set; }
        public string TextureName { get; set; }
        public string AlphaName { get; set; }
        public uint AlphaFlags { get; set; }
        public uint TextureFormat { get; set; }
        public string TextureFormatString
        {
            get =>
                TextureDataLookup.TextureFormats.ContainsKey(TextureFormat) ?
                TextureDataLookup.TextureFormats[TextureFormat] :
                string.Join("", BitConverter.GetBytes(TextureFormat).Select((ccByte) => (char)ccByte));
            set 
            {
                TextureFormat = BitConverter.ToUInt32(value.Select(character => (byte)character).ToArray(), 0);
            }
        }

        public ushort Width { get; set; }
        public ushort Height { get; set; }
        public byte Depth { get; set; }
        public byte MipMapCount { get; set; }
        public byte TexCodeType { get; set; }
        public byte Flags { get; set; }
        public byte[] Pallette { get; set; }
        public uint DataSize { get; set; }
        public byte[] Data { get; set; }
        public List<MipMap> MipMaps { get; set; }


        public uint ContentByteCount => (uint)(
            4 + 4 + 32 + 32 + 
            4 + 4 + 2 + 2 + 1 + 1 + 1 + 1 + 
            GetPaletteByteCount() +
            4 + this.Data.Length +
            this.MipMaps.Sum(mipmap => mipmap.ByteCountWithHeader));
        public uint ByteCount => ContentByteCount;
        public uint ByteCountWithHeader => ByteCount + 12;

        public TextureData()
        {
            this.Header = new ChunkHeader(1);
            this.Version = 0x09;
            this.FilterFlags = 0x1106;
            this.TextureName = "TextureName";
            this.AlphaName = "";
            this.AlphaFlags = 0x8200;
            this.TextureFormatString = "DXT1";
            this.Depth = 16;
            this.TexCodeType = 4;
            this.Flags = 8;
            this.MipMaps = new List<MipMap>();
            this.Pallette = new byte[0];
        }

        public TextureData Read(Stream stream)
        {
            try
            {
                this.Header = new ChunkHeader().Read(stream);
                long dataEnd = stream.Position + this.Header.Size;

                this.Version = RenderWareFileHelper.ReadUint32(stream);
                this.FilterFlags = RenderWareFileHelper.ReadUint32(stream);
                this.TextureName = string.Join("", RenderWareFileHelper.ReadChars(stream, 32));
                this.AlphaName = string.Join("", RenderWareFileHelper.ReadChars(stream, 32));

                this.AlphaFlags = RenderWareFileHelper.ReadUint32(stream);
                this.TextureFormat = RenderWareFileHelper.ReadUint32(stream);
                this.Width = RenderWareFileHelper.ReadUint16(stream);
                this.Height = RenderWareFileHelper.ReadUint16(stream);
                this.Depth = RenderWareFileHelper.ReadByte(stream);
                this.MipMapCount = RenderWareFileHelper.ReadByte(stream);
                this.TexCodeType = RenderWareFileHelper.ReadByte(stream);
                this.Flags = RenderWareFileHelper.ReadByte(stream);

                long paletteStart = stream.Position;
                this.Pallette = new byte[GetPaletteByteCount(dataEnd, paletteStart)];
                for (int i = 0; i < this.Pallette.Length; i++)
                {
                    this.Pallette[i] = RenderWareFileHelper.ReadByte(stream);
                }

                this.DataSize = RenderWareFileHelper.ReadUint32(stream);
                if (this.DataSize > int.MaxValue ||
                    this.DataSize > dataEnd - stream.Position)
                {
                    throw new IOException(
                        $"Texture data size {this.DataSize} exceeds the texture chunk bounds.");
                }

                this.Data = new byte[(int)this.DataSize];
                int bytesRead = stream.Read(this.Data, 0, this.Data.Length);

                if (bytesRead != this.Data.Length)
                {
                    throw new IOException(
                        $"Unable to read texture data '{this.TextureName}' " +
                        $"from memory stream. Expected {this.Data.Length} bytes, read {bytesRead}.");
                }

                int mipMapCount = Math.Max(0, this.MipMapCount - 1);
                this.MipMaps = RenderWareFileHelper.ReadBinaryStructure<MipMap>(stream, mipMapCount);

                stream.Position = dataEnd;
            }
            catch (Exception e)
            {
                Debug.LogWarning(e);
            }

            return this;
        }

        private int GetPaletteByteCount()
        {
            return this.Pallette?.Length ?? 0;
        }

        private int GetPaletteByteCount(long dataEnd, long paletteStart)
        {
            if (this.Depth != 4 && this.Depth != 8)
            {
                return 0;
            }

            long imageDataSize = GetImageDataSize(
                this.Width,
                this.Height,
                this.Depth,
                this.TextureFormat);
            long mipMapByteCount = GetMipMapByteCount();

            if (imageDataSize >= 0 && mipMapByteCount >= 0)
            {
                long dataSizePosition = dataEnd - 4 - imageDataSize - mipMapByteCount;
                long paletteByteCount = dataSizePosition - paletteStart;

                if (paletteByteCount >= 0 &&
                    paletteByteCount <= int.MaxValue &&
                    paletteByteCount % 4 == 0)
                {
                    return (int)paletteByteCount;
                }
            }

            return GetDefaultPaletteByteCount();
        }

        private int GetDefaultPaletteByteCount()
        {
            if (this.Depth == 8)
            {
                return 256 * 4;
            }

            if (this.Depth == 4)
            {
                return 16 * 4;
            }

            return 0;
        }

        private long GetMipMapByteCount()
        {
            long byteCount = 0;
            int width = this.Width;
            int height = this.Height;

            for (int mipMapIndex = 1; mipMapIndex < this.MipMapCount; mipMapIndex++)
            {
                width = Math.Max(1, width / 2);
                height = Math.Max(1, height / 2);

                long mipMapDataSize = GetImageDataSize(
                    width,
                    height,
                    this.Depth,
                    this.TextureFormat);

                if (mipMapDataSize < 0)
                {
                    return -1;
                }

                byteCount += 4 + mipMapDataSize;
            }

            return byteCount;
        }

        private static long GetImageDataSize(
            int width,
            int height,
            byte depth,
            uint textureFormat)
        {
            long blockWidth = Math.Max(1, (width + 3) / 4);
            long blockHeight = Math.Max(1, (height + 3) / 4);

            if (textureFormat == FourCc("DXT1"))
            {
                return blockWidth * blockHeight * 8;
            }

            if (textureFormat == FourCc("DXT3") ||
                textureFormat == FourCc("DXT5"))
            {
                return blockWidth * blockHeight * 16;
            }

            long pixelCount = (long)width * height;

            switch (depth)
            {
                case 4:
                    return (pixelCount + 1) / 2;
                case 8:
                    return pixelCount;
                case 16:
                    return pixelCount * 2;
                case 24:
                    return pixelCount * 3;
                case 32:
                    return pixelCount * 4;
                default:
                    return -1;
            }
        }

        private static uint FourCc(string value)
        {
            return BitConverter.ToUInt32(Encoding.ASCII.GetBytes(value), 0);
        }

        public void Write(Stream stream)
        {
            this.Header.Size = ByteCount;

            this.Header.Write(stream);

            RenderWareFileHelper.WriteUint32(stream, this.Version);
            RenderWareFileHelper.WriteUint32(stream, this.FilterFlags);
            RenderWareFileHelper.WriteChars(stream, this.TextureName.PadRight(32, '\0').ToCharArray());
            RenderWareFileHelper.WriteChars(stream, this.AlphaName.PadRight(32, '\0').ToCharArray());

            RenderWareFileHelper.WriteUint32(stream, this.AlphaFlags);
            RenderWareFileHelper.WriteUint32(stream, this.TextureFormat);
            RenderWareFileHelper.WriteUint16(stream, this.Width);
            RenderWareFileHelper.WriteUint16(stream, this.Height);
            RenderWareFileHelper.WriteByte(stream, this.Depth);
            RenderWareFileHelper.WriteByte(stream, (byte)(this.MipMaps.Count + 1));
            RenderWareFileHelper.WriteByte(stream, this.TexCodeType);
            RenderWareFileHelper.WriteByte(stream, this.Flags);

            foreach(byte palletteByte in this.Pallette)
            {
                RenderWareFileHelper.WriteByte(stream, palletteByte);
            }

            RenderWareFileHelper.WriteUint32(stream, (uint)this.Data.Length);
            //foreach (byte dataByte in this.Data)
            //{
            //    RenderWareFileHelper.WriteByte(stream, dataByte);
            //}
            stream.Write(this.Data, 0, this.Data.Length);
            RenderWareFileHelper.WriteBinaryStructure(stream, this.MipMaps);

        }

        public byte[] GetDds(bool withMipMaps = false)
        {
            byte[][] mipmaps = null;
            if (withMipMaps)
            {
                mipmaps = new byte[this.MipMapCount - 1][];
                for (int i = 0; i < this.MipMapCount - 1; i++)
                {
                    mipmaps[i] = this.MipMaps[i].Data;
                }
            }
            return DdsHelper.GetDdsBytes(this.Data, this.TextureFormat, this.Width, this.Height, mipmaps);
        }

        public void SetDds(byte[] dds, bool withMipMaps = false)
        {

            var strippedDds = DdsHelper.StripDdsHeader(dds, out uint fourCC, out uint width, out uint height);

            int mainSize = (int)(width * height / 2);
            var mainData = strippedDds.Take(mainSize);

            this.Data = mainData.ToArray();
            this.TextureFormat = fourCC;
            this.Width = (ushort)width;
            this.Height = (ushort)height;

            if (withMipMaps)
            {
                int offset = mainSize;
                int previousSize = mainSize;

                while (offset < strippedDds.Length)
                {
                    int mipmapSize = previousSize;
                    int remainder = strippedDds.Length - offset;
                    if (remainder < previousSize)
                    {
                        mipmapSize = previousSize / 4;
                    }

                    var mipmap = strippedDds.Skip(offset).Take(mipmapSize).ToArray();
                    this.MipMaps.Add(new MipMap()
                    {
                        Data = mipmap
                    });

                    offset += mipmapSize;
                    previousSize = mipmapSize;
                }
            }
        }
    }
}

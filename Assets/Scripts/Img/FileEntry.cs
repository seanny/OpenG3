using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GTA3Unity.Img
{
    public sealed class FileEntry
    {
        public string FileName { get; private set; }
        public string FileNameWithoutExtension => Path.GetFileNameWithoutExtension(FileName);
        public string FilePath { get; private set; }
        public int Offset { get; private set; }
        public int Size { get; private set; }
        public BufferReader Reader
        {
            get
            {
                m_Reader.Position = Offset;
                return m_Reader;
            }
        }

        private static readonly Dictionary<BufferReader, int> s_Dependents = new();

        private readonly BufferReader m_Reader;

        public FileEntry(string filePath)
        {
            Offset = 0;
            Size = (int)new FileInfo(filePath).Length;
            FilePath = filePath;
            FileName = Path.GetFileName(FilePath);
            // FileEntry only reads the archive. Allow other readers to open it too;
            // the FileStream(path, FileMode.Open) overload requests an exclusive
            // handle, which causes a sharing violation when the archive is loaded
            // more than once or is already open for reading elsewhere.
            m_Reader = new BufferReader(new FileStream(
                FilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read));
            s_Dependents[m_Reader] = 1;
        }

        public FileEntry(FileEntry file, string virtualFileName, int offset, int size)
        {
            Size = size;
            Offset = file.Offset + offset;
            FilePath = file.FilePath;
            FileName = virtualFileName;

            long position = file.m_Reader.Position;
            file.m_Reader.Position = Offset;
            byte[] data = file.m_Reader.ReadBytes(Size);
            file.m_Reader.Position = position;
            m_Reader = new BufferReader(new MemoryStream(data));
            Offset = 0;
            s_Dependents[m_Reader] = 1;
        }

        ~FileEntry()
        {
            if (--s_Dependents[m_Reader] > 0)
            {
                return;
            }

            try
            {
                s_Dependents.Remove(m_Reader);
                m_Reader.Dispose();
            }
            catch (IOException exception)
            {
                Debug.LogError($"Failed to close stream for file '{FilePath}': {exception.Message}");
            }
        }

        public byte[] GetData()
        {
            m_Reader.Position = Offset;
            return m_Reader.ReadBytes(Size);
        }
    }
}

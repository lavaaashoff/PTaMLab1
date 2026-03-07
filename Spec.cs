using System.IO;

namespace PSConsole
{
    struct SpecRecord
    {
        int Next;
        int ComponentRef; // смещение компонента
        int Count;
        byte Deleted;
    }

    class SpecFile
    {
        FileStream fs;
        BinaryReader br;
        BinaryWriter bw;

        public SpecFile(string file)
        {
            fs = new FileStream(file, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            br = new BinaryReader(fs);
            bw = new BinaryWriter(fs);
        }

        public int Add(int componentRef, int count, int next)
        {
            fs.Seek(0, SeekOrigin.End);
            int offset = (int)fs.Position;

            bw.Write(next);
            bw.Write(componentRef);
            bw.Write(count);
            bw.Write((byte)0);

            return offset;
        }
    }
}
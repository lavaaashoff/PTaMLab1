using System;
using System.IO;
using System.Collections.Generic;

namespace PSConsole.FileIO
{
    public class TruncateService
    {
        private readonly FileContext _ctx;

        public TruncateService(FileContext ctx) => _ctx = ctx;

        public void Truncate()
        {
            var (len, head, _) = _ctx.ReadHeaderFull();
            int recordSize = 1 + 4 + 4 + 1 + len;

            // Собираем живые компоненты в порядке обхода.
            var compMap = new Dictionary<int, int>();
            var order = new List<int>();

            int cur = head;
            while (cur != -1)
            {
                _ctx.CompFs.Seek(cur, SeekOrigin.Begin);
                byte del = _ctx.CompReader.ReadByte();
                _ctx.CompReader.ReadInt32();
                int next = _ctx.CompReader.ReadInt32();
                _ctx.CompReader.ReadByte();
                _ctx.CompReader.ReadBytes(len);

                if (del == 0) order.Add(cur);
                cur = next;
            }

            // Новые смещения компонентов (заголовок = 28 байт).
            int newCompOffset = 28;
            foreach (var old in order)
            {
                compMap[old] = newCompOffset;
                newCompOffset += recordSize;
            }

            // Собираем живые спецификации.
            const int specRecordSize = 1 + 4 + 2 + 4;
            long specFileLen = _ctx.SpecFs.Length;
            var specMap = new Dictionary<int, int>();
            var specOrder = new List<int>();

            int specOffset = 8;
            while (specOffset < specFileLen)
            {
                _ctx.SpecFs.Seek(specOffset, SeekOrigin.Begin);
                byte del = _ctx.SpecReader.ReadByte();
                _ctx.SpecReader.ReadInt32();

                if (del == 0) specOrder.Add(specOffset);
                specOffset += specRecordSize;
            }

            int newSpecOffset = 8;
            foreach (var old in specOrder)
            {
                specMap[old] = newSpecOffset;
                newSpecOffset += specRecordSize;
            }

            // Пишем временный .prd.
            string tempComp = _ctx.CurrentFile + ".tmp";
            using (var fs = new FileStream(tempComp, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write((byte)'P');
                bw.Write((byte)'S');
                bw.Write(len);

                int newHead = order.Count > 0 ? compMap[order[0]] : -1;
                bw.Write(newHead);
                bw.Write(newCompOffset);

                byte[] specNameBytes = new byte[16];
                _ctx.CompFs.Seek(12, SeekOrigin.Begin);
                _ctx.CompFs.Read(specNameBytes);
                bw.Write(specNameBytes);

                for (int i = 0; i < order.Count; i++)
                {
                    int oldOff = order[i];
                    _ctx.CompFs.Seek(oldOff, SeekOrigin.Begin);
                    _ctx.CompReader.ReadByte();
                    int oldSpec = _ctx.CompReader.ReadInt32();
                    _ctx.CompReader.ReadInt32();
                    byte type = _ctx.CompReader.ReadByte();
                    byte[] name = _ctx.CompReader.ReadBytes(len);

                    int newNext = (i + 1 < order.Count) ? compMap[order[i + 1]] : -1;
                    int newSpec = (oldSpec != -1 && specMap.ContainsKey(oldSpec)) ? specMap[oldSpec] : -1;

                    bw.Write((byte)0);
                    bw.Write(newSpec);
                    bw.Write(newNext);
                    bw.Write(type);
                    bw.Write(name);
                }

                bw.Flush();
            }

            // Пишем временный .prs.
            string tempSpec = _ctx.SpecFile + ".tmp";
            using (var fs = new FileStream(tempSpec, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(-1);
                bw.Write(newSpecOffset);

                foreach (var oldOff in specOrder)
                {
                    _ctx.SpecFs.Seek(oldOff, SeekOrigin.Begin);
                    _ctx.SpecReader.ReadByte();
                    int oldComp = _ctx.SpecReader.ReadInt32();
                    short count = _ctx.SpecReader.ReadInt16();
                    int oldNext = _ctx.SpecReader.ReadInt32();

                    int newComp = compMap.ContainsKey(oldComp) ? compMap[oldComp] : oldComp;
                    int newNext = (oldNext != -1 && specMap.ContainsKey(oldNext)) ? specMap[oldNext] : -1;

                    bw.Write((byte)0);
                    bw.Write(newComp);
                    bw.Write(count);
                    bw.Write(newNext);
                }

                bw.Flush();
            }

            // Атомарная замена файлов.
            string compFile = _ctx.CurrentFile;
            string specFile = _ctx.SpecFile;

            _ctx.Close();

            File.Delete(compFile);
            File.Move(tempComp, compFile);

            File.Delete(specFile);
            File.Move(tempSpec, specFile);

            _ctx.Open(compFile);

            Console.WriteLine("Физическое удаление завершено.");
        }
    }
}
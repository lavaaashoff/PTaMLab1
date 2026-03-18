using System;
using System.IO;
using System.Collections.Generic;
using PSConsole.Models;

namespace PSConsole.FileIO
{
    public class ComponentRepository
    {
        private readonly FileContext _ctx;

        public ComponentRepository(FileContext ctx) => _ctx = ctx;

        public int FindComponent(string name)
        {
            var (len, head) = _ctx.ReadHeader();

            int cur = head;
            while (cur != -1)
            {
                _ctx.CompFs.Seek(cur, SeekOrigin.Begin);
                byte del = _ctx.CompReader.ReadByte();
                _ctx.CompReader.ReadInt32(); // specHead
                int next = _ctx.CompReader.ReadInt32();
                _ctx.CompReader.ReadByte();  // type
                string curName = new string(_ctx.CompReader.ReadChars(len)).Trim('\0', ' ');

                if (curName == name && del == 0) return cur;

                cur = next;
            }

            return -1;
        }

        /// Возвращает true, если на компонент ссылается хотя бы одна активная спецификация.
        public bool IsComponentReferenced(int componentOffset)
        {
            if (_ctx.SpecFs == null) return false;

            var (len, head) = _ctx.ReadHeader();
            int cur = head;

            while (cur != -1)
            {
                _ctx.CompFs.Seek(cur, SeekOrigin.Begin);
                byte del = _ctx.CompReader.ReadByte();
                int specHead = _ctx.CompReader.ReadInt32();
                int next = _ctx.CompReader.ReadInt32();
                _ctx.CompReader.ReadByte();
                _ctx.CompReader.ReadChars(len);

                if (del == 0 && specHead != -1)
                {
                    int specCur = specHead;
                    while (specCur != -1)
                    {
                        _ctx.SpecFs.Seek(specCur, SeekOrigin.Begin);
                        byte specDel = _ctx.SpecReader.ReadByte();
                        int comp = _ctx.SpecReader.ReadInt32();
                        _ctx.SpecReader.ReadInt16();
                        int specNext = _ctx.SpecReader.ReadInt32();

                        if (comp == componentOffset && specDel == 0) return true;

                        specCur = specNext;
                    }
                }

                cur = next;
            }

            return false;
        }

        // Input
        /// Добавляет новый компонент в начало связного списка.
        public void AddComponent(string name, ComponentType type)
        {
            var (len, head, free) = _ctx.ReadHeaderFull();

            _ctx.CompFs.Seek(free, SeekOrigin.Begin);
            int offset = (int)_ctx.CompFs.Position;

            _ctx.CompWriter.Write((byte)0);   // del
            _ctx.CompWriter.Write(-1);         // specHead
            _ctx.CompWriter.Write(head);       // next
            _ctx.CompWriter.Write((byte)type);

            byte[] data = new byte[len];
            byte[] src = System.Text.Encoding.ASCII.GetBytes(name);
            int copy = Math.Min(src.Length, len);
            Array.Copy(src, data, copy);
            for (int i = copy; i < len; i++) data[i] = (byte)' ';
            _ctx.CompWriter.Write(data);

            _ctx.WriteHead(offset);
            _ctx.WriteFree(offset + 1 + 4 + 4 + 1 + len);
        }

        public void AddSpec(string parent, string child)
        {
            int parentOffset = FindComponent(parent);
            int childOffset = FindComponent(child);

            if (parentOffset == -1 || childOffset == -1)
                throw new Exception("Компонент не найден.");

            if (parentOffset == childOffset)
                throw new Exception("Компонент не может ссылаться сам на себя.");

            _ctx.CompFs.Seek(parentOffset, SeekOrigin.Begin);
            _ctx.CompReader.ReadByte();
            int specHead = _ctx.CompReader.ReadInt32();
            _ctx.CompReader.ReadInt32();
            byte parentType = _ctx.CompReader.ReadByte();

            if ((ComponentType)parentType == ComponentType.Detail)
                throw new Exception("Деталь не может иметь спецификаций.");

            _ctx.CompFs.Seek(childOffset, SeekOrigin.Begin);
            _ctx.CompReader.ReadByte();
            _ctx.CompReader.ReadInt32();
            _ctx.CompReader.ReadInt32();
            // byte childType = _ctx.CompReader.ReadByte(); // зарезервировано

            // Ищем существующую запись.
            int cur = specHead;
            while (cur != -1)
            {
                _ctx.SpecFs.Seek(cur, SeekOrigin.Begin);
                byte del = _ctx.SpecReader.ReadByte();
                int comp = _ctx.SpecReader.ReadInt32();
                short count = _ctx.SpecReader.ReadInt16();
                int next = _ctx.SpecReader.ReadInt32();

                if (comp == childOffset && del == 0)
                {
                    _ctx.SpecFs.Seek(cur + 5, SeekOrigin.Begin);
                    _ctx.SpecWriter.Write((short)(count + 1));
                    Console.WriteLine($"Кратность увеличена ({count + 1}).");
                    return;
                }

                cur = next;
            }

            // Создаём новую запись.
            _ctx.SpecFs.Seek(0, SeekOrigin.End);
            int newPos = (int)_ctx.SpecFs.Position;
            _ctx.SpecWriter.Write((byte)0);
            _ctx.SpecWriter.Write(childOffset);
            _ctx.SpecWriter.Write((short)1);
            _ctx.SpecWriter.Write(specHead);

            int newFree = (int)_ctx.SpecFs.Position;
            _ctx.SpecFs.Seek(4, SeekOrigin.Begin);
            _ctx.SpecWriter.Write(newFree);

            _ctx.CompFs.Seek(parentOffset + 1, SeekOrigin.Begin);
            _ctx.CompWriter.Write(newPos);

            Console.WriteLine("Спецификация добавлена.");
        }

        public void DeleteComponent(string name)
        {
            var (len, head) = _ctx.ReadHeader();
            int cur = head;

            while (cur != -1)
            {
                _ctx.CompFs.Seek(cur, SeekOrigin.Begin);
                int delPos = (int)_ctx.CompFs.Position;
                byte deleted = _ctx.CompReader.ReadByte();
                int spec = _ctx.CompReader.ReadInt32();
                int next = _ctx.CompReader.ReadInt32();
                _ctx.CompReader.ReadByte();
                string curName = new string(_ctx.CompReader.ReadChars(len)).Trim('\0', ' ');

                if (curName == name && deleted == 0)
                {
                    if (IsComponentReferenced(cur))
                        throw new Exception($"Невозможно удалить '{name}': на него есть ссылки в спецификациях.");

                    _ctx.CompFs.Seek(delPos, SeekOrigin.Begin);
                    _ctx.CompWriter.Write((byte)1);

                    if (spec != -1) DeleteSpecChain(spec);

                    Console.WriteLine("Запись помечена как удалённая (вместе со спецификациями).");
                    return;
                }

                cur = next;
            }

            Console.WriteLine("Компонент не найден.");
        }

        public void DeleteSpec(string parent, string child)
        {
            int parentOffset = FindComponent(parent);
            int childOffset = FindComponent(child);

            if (parentOffset == -1 || childOffset == -1)
                throw new Exception("Компонент не найден.");

            _ctx.CompFs.Seek(parentOffset + 1, SeekOrigin.Begin);
            int specHead = _ctx.CompReader.ReadInt32();

            if (specHead == -1)
                throw new Exception("У компонента нет спецификаций.");

            int cur = specHead;
            int prev = -1;

            while (cur != -1)
            {
                _ctx.SpecFs.Seek(cur, SeekOrigin.Begin);
                byte del = _ctx.SpecReader.ReadByte();
                int comp = _ctx.SpecReader.ReadInt32();
                short count = _ctx.SpecReader.ReadInt16();
                int next = _ctx.SpecReader.ReadInt32();

                if (comp == childOffset && del == 0)
                {
                    if (count > 1)
                    {
                        _ctx.SpecFs.Seek(cur + 5, SeekOrigin.Begin);
                        _ctx.SpecWriter.Write((short)(count - 1));
                        Console.WriteLine("Кратность уменьшена.");
                    }
                    else
                    {
                        _ctx.SpecFs.Seek(cur, SeekOrigin.Begin);
                        _ctx.SpecWriter.Write((byte)1);

                        if (prev == -1)
                        {
                            _ctx.CompFs.Seek(parentOffset + 1, SeekOrigin.Begin);
                            _ctx.CompWriter.Write(next);
                        }
                        else
                        {
                            _ctx.SpecFs.Seek(prev + 1 + 4 + 2, SeekOrigin.Begin);
                            _ctx.SpecWriter.Write(next);
                        }

                        Console.WriteLine("Спецификация помечена как удалённая.");
                    }

                    return;
                }

                prev = cur;
                cur = next;
            }

            Console.WriteLine("Спецификация не найдена.");
        }

        private void DeleteSpecChain(int specHead)
        {
            int cur = specHead;
            while (cur != -1)
            {
                _ctx.SpecFs.Seek(cur, SeekOrigin.Begin);
                byte del = _ctx.SpecReader.ReadByte();
                _ctx.SpecReader.ReadInt32();
                _ctx.SpecReader.ReadInt16();
                int next = _ctx.SpecReader.ReadInt32();

                if (del == 0)
                {
                    _ctx.SpecFs.Seek(cur, SeekOrigin.Begin);
                    _ctx.SpecWriter.Write((byte)1);
                }

                cur = next;
            }
        }

        public void Restore(string name)
        {
            var (len, head) = _ctx.ReadHeader();
            int cur = head;

            while (cur != -1)
            {
                _ctx.CompFs.Seek(cur, SeekOrigin.Begin);
                byte deleted = _ctx.CompReader.ReadByte();
                int spec = _ctx.CompReader.ReadInt32();
                int next = _ctx.CompReader.ReadInt32();
                _ctx.CompReader.ReadByte();
                string curName = new string(_ctx.CompReader.ReadChars(len)).Trim('\0', ' ');

                if (curName == name && deleted == 1)
                {
                    _ctx.CompFs.Seek(cur, SeekOrigin.Begin);
                    _ctx.CompWriter.Write((byte)0);

                    if (spec != -1) RestoreSpecChain(spec);

                    Console.WriteLine("Запись восстановлена.");
                    return;
                }

                cur = next;
            }

            Console.WriteLine("Удалённая запись не найдена.");
        }

        public int RestoreAll()
        {
            var (_, head) = _ctx.ReadHeader();
            int count = 0;
            int cur = head;

            while (cur != -1)
            {
                _ctx.CompFs.Seek(cur, SeekOrigin.Begin);
                byte deleted = _ctx.CompReader.ReadByte();
                int spec = _ctx.CompReader.ReadInt32();
                int next = _ctx.CompReader.ReadInt32();
                _ctx.CompReader.ReadByte();

                if (deleted == 1)
                {
                    _ctx.CompFs.Seek(cur, SeekOrigin.Begin);
                    _ctx.CompWriter.Write((byte)0);

                    if (spec != -1) RestoreSpecChain(spec);

                    count++;
                }

                cur = next;
            }

            return count;
        }

        private void RestoreSpecChain(int specHead)
        {
            int cur = specHead;
            while (cur != -1)
            {
                _ctx.SpecFs.Seek(cur, SeekOrigin.Begin);
                byte del = _ctx.SpecReader.ReadByte();
                _ctx.SpecReader.ReadInt32();
                _ctx.SpecReader.ReadInt16();
                int next = _ctx.SpecReader.ReadInt32();

                if (del == 1)
                {
                    _ctx.SpecFs.Seek(cur, SeekOrigin.Begin);
                    _ctx.SpecWriter.Write((byte)0);
                }

                cur = next;
            }
        }


        public void PrintAll()
        {
            var (len, head) = _ctx.ReadHeader();
            int cur = head;
            bool found = false;

            Console.WriteLine("Список всех компонентов:");
            while (cur != -1)
            {
                _ctx.CompFs.Seek(cur, SeekOrigin.Begin);
                byte del = _ctx.CompReader.ReadByte();
                _ctx.CompReader.ReadInt32();
                int next = _ctx.CompReader.ReadInt32();
                byte type = _ctx.CompReader.ReadByte();
                string name = new string(_ctx.CompReader.ReadChars(len)).Trim('\0');

                if (del == 0)
                {
                    Console.WriteLine($"{name,-20} {(ComponentType)type}");
                    found = true;
                }

                cur = next;
            }

            if (!found) Console.WriteLine("Компоненты не найдены.");
        }

        public void Print(string name)
        {
            var (len, head) = _ctx.ReadHeader();
            int cur = head;

            while (cur != -1)
            {
                _ctx.CompFs.Seek(cur, SeekOrigin.Begin);
                byte del = _ctx.CompReader.ReadByte();
                int spec = _ctx.CompReader.ReadInt32();
                int next = _ctx.CompReader.ReadInt32();
                byte type = _ctx.CompReader.ReadByte();
                string curName = new string(_ctx.CompReader.ReadChars(len)).Trim('\0', ' ');

                if (curName == name && del == 0)
                {
                    Console.WriteLine($"Компонент: {curName}");
                    Console.WriteLine($"Тип: {(ComponentType)type}");

                    if (spec != -1)
                    {
                        Console.WriteLine("Спецификации:");
                        PrintSpecifications(spec, len);
                    }
                    else
                    {
                        Console.WriteLine("Спецификации: нет");
                    }

                    return;
                }

                cur = next;
            }

            Console.WriteLine("Компонент не найден.");
        }

        private void PrintSpecifications(int head, int len)
        {
            int cur = head;
            while (cur != -1)
            {
                _ctx.SpecFs.Seek(cur, SeekOrigin.Begin);
                byte del = _ctx.SpecReader.ReadByte();
                int comp = _ctx.SpecReader.ReadInt32();
                short count = _ctx.SpecReader.ReadInt16();
                int next = _ctx.SpecReader.ReadInt32();

                if (del == 0)
                {
                    _ctx.CompFs.Seek(comp, SeekOrigin.Begin);
                    _ctx.CompReader.ReadByte();
                    _ctx.CompReader.ReadInt32();
                    _ctx.CompReader.ReadInt32();
                    byte typeComp = _ctx.CompReader.ReadByte();
                    string compName = new string(_ctx.CompReader.ReadChars(len)).Trim('\0', ' ');

                    Console.WriteLine($"  - {compName} ({(ComponentType)typeComp}) x{count}");
                }

                cur = next;
            }
        }
    }
}
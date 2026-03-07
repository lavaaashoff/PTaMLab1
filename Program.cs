using Microsoft.VisualBasic.FileIO;
using System;
using System.IO;
using System.Reflection.PortableExecutable;

namespace PSConsole
{
    class Program
    {
        static FileStream compFs;
        static BinaryReader compReader;
        static BinaryWriter compWriter;
        static string currentFile;
        static FileStream specFs;
        static BinaryReader specReader;
        static BinaryWriter specWriter;
        static string specFile;

        public enum ComponentType : byte
        {
            Product = 1,
            Unit = 2,
            Detail = 3
        }

        static void Main()
        {
            while (true)
            {
                Console.Write("PS> ");
                var cmd = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(cmd)) continue;

                var parts = cmd.Split(' ', 2);
                var command = parts[0];
                var args = parts.Length > 1 ? parts[1] : "";

                switch (command)
                {
                    case "Create":
                        Create(args);
                        break;
                    case "Open":
                        Open(args);
                        break;
                    case "Input":
                        Input(args);
                        break;
                    case "Delete":
                        Delete(args);
                        break;

                    case "Restore":
                        Restore(args);
                        break;
                    case "Truncate":
                        Truncate();
                        break;
                    case "Print":
                        Print(args);
                        break;
                    case "Exit":
                        Close();
                        return;
                    default:
                        Console.WriteLine("Неизвестная команда");
                        break;
                }
            }
        }

        static void Create(string args)
        {
            var name = args[..args.IndexOf("(")];
            var len = short.Parse(args[(args.IndexOf("(") + 1)..args.IndexOf(")")]);

            // файл компонентов
            using (var fs = new FileStream(name, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write((byte)'P');
                bw.Write((byte)'S');
                bw.Write(len);
                bw.Write(-1);
                bw.Write((int)fs.Position + 4);
            }

            // файл спецификаций
            string prs = Path.ChangeExtension(name, ".prs");

            using (var fs = new FileStream(prs, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(-1); // first
                bw.Write(8);  // free
            }

            Console.WriteLine("Файлы PS и PRS созданы");
        }

        static void Open(string name)
        {
            currentFile = name;
            specFile = Path.ChangeExtension(name, ".prs");

            compFs = new FileStream(name, FileMode.Open, FileAccess.ReadWrite);
            compReader = new BinaryReader(compFs);
            compWriter = new BinaryWriter(compFs);

            specFs = new FileStream(specFile, FileMode.Open, FileAccess.ReadWrite);
            specReader = new BinaryReader(specFs);
            specWriter = new BinaryWriter(specFs);

            if (compReader.ReadByte() != 'P' || compReader.ReadByte() != 'S')
            {
                Console.WriteLine("Ошибка сигнатуры");
                Close();
                return;
            }

            Console.WriteLine("Файлы открыты");
        }

        static void Input(string args)
        {

            if (args.Contains("/"))
            {
                InputSpec(args);
                return;
            }

            var inside = args[(args.IndexOf("(") + 1)..args.IndexOf(")")];
            var split = inside.Split(',');

            string name = split[0].Trim();
            ComponentType type = Enum.Parse<ComponentType>(split[1].Trim(), true);

            compFs.Seek(2, SeekOrigin.Begin);
            short maxLen = compReader.ReadInt16();
            int head = compReader.ReadInt32();

            compFs.Seek(0, SeekOrigin.End);
            int offset = (int)compFs.Position;

            compWriter.Write(head);       // Next
            compWriter.Write(-1);         // SpecHead
            compWriter.Write((byte)0);    // Deleted
            compWriter.Write((byte)type); // Type

            char[] buf = new char[maxLen];
            name.CopyTo(0, buf, 0, name.Length);
            compWriter.Write(buf);

            compFs.Seek(4, SeekOrigin.Begin);
            compWriter.Write(offset);

            Console.WriteLine("Компонент добавлен");
        }

        static void InputSpec(string args)
        {
            var inside = args[(args.IndexOf("(") + 1)..args.IndexOf(")")];
            var split = inside.Split('/');

            string parent = split[0].Trim();
            string child = split[1].Trim();

            int parentOffset = FindComponent(parent);
            int childOffset = FindComponent(child);

            if (parentOffset == -1 || childOffset == -1)
            {
                Console.WriteLine("Компонент не найден");
                return;
            }

            // читаем голову списка
            specFs.Seek(0, SeekOrigin.Begin);
            int head = specReader.ReadInt32();

            int cur = head;

            while (cur != -1)
            {
                specFs.Seek(cur, SeekOrigin.Begin);

                byte del = specReader.ReadByte();
                int comp = specReader.ReadInt32();
                short count = specReader.ReadInt16();
                int next = specReader.ReadInt32();

                // если уже есть такой child → увеличиваем count
                if (comp == childOffset && del == 0)
                {
                    specFs.Seek(cur + 5, SeekOrigin.Begin);
                    specWriter.Write((short)(count + 1));

                    Console.WriteLine("Кратность увеличена");
                    return;
                }

                cur = next;
            }

            // если записи нет → создаём новую ВРТ ЗДЕСЬ КОСЯК В СПЕЦИФИК НЕ ДОБАВЛ
            specFs.Seek(0, SeekOrigin.End);

            specWriter.Write((byte)0);
            specWriter.Write(childOffset);
            specWriter.Write((short)1);
            specWriter.Write(head);
            int last = (int)specFs.Position;

            // новая запись становится головой
            specFs.Seek(0, SeekOrigin.Begin);
            specWriter.Write(8);
            specWriter.Write(last);

            Console.WriteLine("Спецификация добавлена");
        }

        static int FindComponent(string name)
        {
            compFs.Seek(2, SeekOrigin.Begin);
            short len = compReader.ReadInt16();
            int head = compReader.ReadInt32();

            while (head != -1)
            {
                compFs.Seek(head, SeekOrigin.Begin);

                int next = compReader.ReadInt32();
                compReader.ReadInt32();
                byte del = compReader.ReadByte();
                compReader.ReadByte();
                string cur = new string(compReader.ReadChars(len)).Trim('\0');

                if (cur == name && del == 0)
                    return head;

                head = next;
            }

            return -1;
        }

        static void Print(string args)
        {
            compFs.Seek(2, SeekOrigin.Begin);
            short len = compReader.ReadInt16();
            int head = compReader.ReadInt32();

            while (head != -1)
            {
                compFs.Seek(head, SeekOrigin.Begin);
                int next = compReader.ReadInt32();
                int spec = compReader.ReadInt32();
                byte del = compReader.ReadByte();
                byte type = compReader.ReadByte();
                string name = new string(compReader.ReadChars(len)).Trim('\0');

                if (del == 0)
                    Console.WriteLine($"{name,-20} {(ComponentType)type}");

                head = next;
            }
        }

        static void Delete(string args)
        {
            string name = args[(args.IndexOf("(") + 1)..args.IndexOf(")")].Trim();

            compFs.Seek(2, SeekOrigin.Begin);
            short len = compReader.ReadInt16();
            int head = compReader.ReadInt32();

            while (head != -1)
            {
                compFs.Seek(head, SeekOrigin.Begin);

                int next = compReader.ReadInt32();
                compReader.ReadInt32(); // SpecHead
                long delPos = compFs.Position;
                byte deleted = compReader.ReadByte();
                compReader.ReadByte(); // Type
                string curName = new string(compReader.ReadChars(len)).Trim('\0');

                if (curName == name && deleted == 0)
                {
                    compFs.Seek(delPos, SeekOrigin.Begin);
                    compWriter.Write((byte)1);
                    Console.WriteLine("Запись помечена как удалённая");
                    return;
                }

                head = next;
            }

            Console.WriteLine("Компонент не найден");
        }

        static void RestoreAll()
        {
            compFs.Seek(2, SeekOrigin.Begin);
            short len = compReader.ReadInt16();
            int head = compReader.ReadInt32();

            int count = 0;

            while (head != -1)
            {
                compFs.Seek(head, SeekOrigin.Begin);

                int next = compReader.ReadInt32();
                compReader.ReadInt32();
                long delPos = compFs.Position;
                byte deleted = compReader.ReadByte();

                if (deleted == 1)
                {
                    compFs.Seek(delPos, SeekOrigin.Begin);
                    compWriter.Write((byte)0);
                    count++;
                }

                head = next;
            }

            Console.WriteLine($"Восстановлено записей: {count}");
        }

        static void Restore(string args)
        {
            if (args.Contains("*"))
            {
                RestoreAll();
                return;
            }

            string name = args[(args.IndexOf("(") + 1)..args.IndexOf(")")].Trim();

            compFs.Seek(2, SeekOrigin.Begin);
            short len = compReader.ReadInt16();
            int head = compReader.ReadInt32();

            while (head != -1)
            {
                compFs.Seek(head, SeekOrigin.Begin);

                int next = compReader.ReadInt32();
                compReader.ReadInt32();
                long delPos = compFs.Position;
                byte deleted = compReader.ReadByte();
                compReader.ReadByte();
                string curName = new string(compReader.ReadChars(len)).Trim('\0');

                if (curName == name && deleted == 1)
                {
                    compFs.Seek(delPos, SeekOrigin.Begin);
                    compWriter.Write((byte)0);
                    Console.WriteLine("Запись восстановлена");
                    return;
                }

                head = next;
            }

            Console.WriteLine("Удалённая запись не найдена");
        }

        static void Truncate()
        {
            string tempFile = currentFile + ".tmp";

            compFs.Seek(2, SeekOrigin.Begin);
            short len = compReader.ReadInt16();
            int head = compReader.ReadInt32();

            using var newFs = new FileStream(tempFile, FileMode.Create);
            using var newBw = new BinaryWriter(newFs);

            newBw.Write((byte)'P');
            newBw.Write((byte)'S');
            newBw.Write(len);
            newBw.Write(-1); // new head
            newBw.Write(-1); // free

            int newHead = -1;
            int newTail = -1;

            while (head != -1)
            {
                compFs.Seek(head, SeekOrigin.Begin);

                int next = compReader.ReadInt32();
                int spec = compReader.ReadInt32();
                byte deleted = compReader.ReadByte();
                byte type = compReader.ReadByte();
                char[] name = compReader.ReadChars(len);

                if (deleted == 0)
                {
                    int pos = (int)newFs.Position;

                    newBw.Write(-1);
                    newBw.Write(spec);
                    newBw.Write((byte)0);
                    newBw.Write(type);
                    newBw.Write(name);

                    if (newHead == -1)
                        newHead = pos;
                    else
                    {
                        newFs.Seek(newTail, SeekOrigin.Begin);
                        newBw.Write(pos);
                    }

                    newTail = pos;
                    newFs.Seek(0, SeekOrigin.End);
                }

                head = next;
            }

            newFs.Seek(4, SeekOrigin.Begin);
            newBw.Write(newHead);
            newBw.Write((int)newFs.Position);

            Close();
            newFs?.Close();
            newBw?.Close();

            File.Delete(currentFile);
            File.Move(tempFile, currentFile);

            Open(currentFile);

            Console.WriteLine("Физическое удаление завершено");
        }

        static void Close()
        {
            compReader?.Close();
            compWriter?.Close();
            compFs?.Close();

            specReader?.Close();
            specWriter?.Close();
            specFs?.Close();
        }
    }
}
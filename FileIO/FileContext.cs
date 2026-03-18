using System;
using System.IO;
using PSConsole.Models;

namespace PSConsole.FileIO
{
    /// Хранит открытые файловые потоки и предоставляет низкоуровневые операции с файлами.
    public class FileContext : IDisposable
    {
        public FileStream CompFs { get; private set; }
        public BinaryReader CompReader { get; private set; }
        public BinaryWriter CompWriter { get; private set; }
        public string CurrentFile { get; private set; }

        public FileStream SpecFs { get; private set; }
        public BinaryReader SpecReader { get; private set; }
        public BinaryWriter SpecWriter { get; private set; }
        public string SpecFile { get; private set; }

        public bool IsOpen => CompFs != null;


        /// Создаёт новую пару файлов (.prd + .prs).
        public bool Create(string filename, string prsFilename, short maxLength)
        {
            if (File.Exists(filename))
            {
                using var fs = new FileStream(filename, FileMode.Open);
                using var br = new BinaryReader(fs);

                if (fs.Length < 2)
                    throw new Exception("Файл существует, но сигнатура отсутствует или не соответствует заданию.");

                if (br.ReadByte() != 'P' || br.ReadByte() != 'S')
                    throw new Exception("Файл существует, но сигнатура не соответствует заданию.");

                Console.Write($"Файл '{filename}' уже существует. Перезаписать? (y/n): ");
                string answer = Console.ReadLine()?.Trim().ToLower();
                if (answer != "y" && answer != "yes" && answer != "да" && answer != "д")
                {
                    Console.WriteLine("Создание файла отменено.");
                    return false;
                }
            }

            // Файл спецификаций (.prs): firstFree=-1, free=8
            using (var fs = new FileStream(prsFilename, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(-1);
                bw.Write(8);
            }

            // Файл компонентов (.prd): сигнатура + maxLen + head(-1) + free(28) + имя prs (16 байт)
            using (var fs = new FileStream(filename, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write((byte)'P');
                bw.Write((byte)'S');
                bw.Write(maxLength);
                bw.Write(-1); // head
                bw.Write(28); // free

                byte[] nameBytes = new byte[16];
                var src = System.Text.Encoding.ASCII.GetBytes(prsFilename);
                Array.Copy(src, nameBytes, Math.Min(src.Length, 16));
                bw.Write(nameBytes);
            }

            return true;
        }


        public bool Open(string name)
        {
            try
            {
                CompFs = new FileStream(name, FileMode.Open, FileAccess.ReadWrite);
                CompReader = new BinaryReader(CompFs);
                CompWriter = new BinaryWriter(CompFs);

                if (CompReader.ReadByte() != 'P' || CompReader.ReadByte() != 'S')
                {
                    Console.WriteLine("Ошибка: сигнатура файла не соответствует заданию.");
                    Close();
                    return false;
                }

                // Пропускаем maxLen, head, free
                CompReader.ReadInt16();
                CompReader.ReadInt32();
                CompReader.ReadInt32();

                byte[] nameBytes = CompReader.ReadBytes(16);
                string prsFromHeader = System.Text.Encoding.ASCII
                    .GetString(nameBytes)
                    .TrimEnd('\0');

                if (string.IsNullOrWhiteSpace(prsFromHeader) || !File.Exists(prsFromHeader))
                {
                    prsFromHeader = Path.ChangeExtension(name, ".prs");
                    if (!File.Exists(prsFromHeader))
                    {
                        Console.WriteLine("Ошибка: файл спецификаций не найден.");
                        Close();
                        return false;
                    }
                    Console.WriteLine($"Предупреждение: использован файл спецификаций по умолчанию '{prsFromHeader}'.");
                }

                SpecFile = prsFromHeader;
                SpecFs = new FileStream(SpecFile, FileMode.Open, FileAccess.ReadWrite);
                SpecReader = new BinaryReader(SpecFs);
                SpecWriter = new BinaryWriter(SpecFs);

                CurrentFile = name;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при открытии: {ex.Message}");
                Close();
                return false;
            }
        }

        public void Close()
        {
            CompReader?.Close(); CompWriter?.Close(); CompFs?.Close();
            CompFs = null; CompReader = null; CompWriter = null;

            SpecReader?.Close(); SpecWriter?.Close(); SpecFs?.Close();
            SpecFs = null; SpecReader = null; SpecWriter = null;
        }

        public void Dispose() => Close();

        // Вспомогательные методы

        // Читает maxLength из заголовка .prd.
        public short ReadMaxLength()
        {
            CompFs.Seek(2, SeekOrigin.Begin);
            return CompReader.ReadInt16();
        }

        // Читает (maxLength, head) из заголовка .prd.
        public (short len, int head) ReadHeader()
        {
            CompFs.Seek(2, SeekOrigin.Begin);
            short len = CompReader.ReadInt16();
            int head = CompReader.ReadInt32();
            return (len, head);
        }

        // Читает (maxLength, head, free) из заголовка .prd.
        public (short len, int head, int free) ReadHeaderFull()
        {
            CompFs.Seek(2, SeekOrigin.Begin);
            short len = CompReader.ReadInt16();
            int head = CompReader.ReadInt32();
            int free = CompReader.ReadInt32();
            return (len, head, free);
        }

        //  Обновляет указатель на первую запись в заголовке .prd.
        public void WriteHead(int head)
        {
            CompFs.Seek(4, SeekOrigin.Begin);
            CompWriter.Write(head);
        }

        // Обновляет указатель свободного места в заголовке .prd.
        public void WriteFree(int free)
        {
            CompFs.Seek(8, SeekOrigin.Begin);
            CompWriter.Write(free);
        }
    }
}
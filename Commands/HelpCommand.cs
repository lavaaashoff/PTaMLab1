using System;
using System.IO;

namespace PSConsole.Commands
{
    public static class HelpCommand
    {
        private static readonly string[] Lines =
        {
            "Доступные команды:",
            "  Create <имя_файла>(<длина_записи>[, <имя_файла_спецификаций>]) - создать новый файл",
            "  Open <имя_файла> - открыть существующий файл компонентов",
            "  Input (компонент, тип) - добавить компонент",
            "    Product (1) - изделие",
            "    Unit (2)    - узел",
            "    Detail (3)  - деталь",
            "  Input (компонент/деталь) - добавить спецификацию",
            "  Delete (компонент) - удалить компонент",
            "  Delete (компонент/деталь) - удалить спецификацию",
            "  Restore * - восстановить все удалённые записи",
            "  Restore (компонент) - восстановить конкретный компонент",
            "  Truncate - физически удалить помеченные записи",
            "  Print * - вывести все компоненты",
            "  Print (компонент) - вывести информацию о компоненте",
            "  Help [файл] - показать справку или сохранить в файл",
            "  Exit - выход из программы",
        };

        public static void Display()
        {
            foreach (var line in Lines)
                Console.WriteLine(line);
        }

        public static void SaveToFile(string filename)
        {
            using var writer = new StreamWriter(filename);
            writer.WriteLine("Справка по командам PSConsole:");
            foreach (var line in Lines)
                writer.WriteLine(line);
        }
    }
}
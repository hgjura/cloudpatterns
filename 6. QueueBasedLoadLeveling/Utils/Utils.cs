using System;
using System.Drawing;
using Alba.CsConsoleFormat;

namespace QueueBasedLoadLeveleing
{
    public static class Utils
    {
        static int[] cColors = {
                        0x000080, //DarkBlue = 1
                        0x008000, //DarkGreen = 2
                        0x008080, //DarkCyan = 3
                        0x800000, //DarkRed = 4
                        0x800080, //DarkMagenta = 5
                        0x808000, //DarkYellow = 6

                        0x808080, //DarkGray = 8
                        0x0000FF, //Blue = 9
                        0x00FF00, //Green = 10
                        0x00FFFF, //Cyan = 11
                        0xFF0000, //Red = 12
                        0xFF00FF, //Magenta = 13
                        0xFFFF00, //Yellow = 14
                    };
        public static Color RandomColor()
        {
            return Color.FromArgb(cColors[new Random().Next(0, cColors.Length-1)]);
        }
        public static Color GetColor(int X)
        {
            return X >= cColors.Length ? Color.FromArgb(cColors[cColors.Length % X]) : Color.FromArgb(cColors[X]);
        }
        public static void ConsoleWriteHeader(string Header, ConsoleColor Color)
        {
            var b = new Border
            {
                Stroke = LineThickness.Single,
                Align = Align.Left
            };
            b.Children.Add(new Span($" {Header} ") { Color = Color });
            ConsoleRenderer.RenderDocument(new Document(b));
        }
    }
}

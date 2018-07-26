using System;
using System.Collections.Generic;

namespace CloudPattternsSamples
{
    public class ConsoleMenu
    {
        private ConsoleColor _def_background_color = ConsoleColor.Blue;
        private ConsoleColor _def_foreground_color = ConsoleColor.White;
        private List<dynamic> _menuBuilder = new List<dynamic>();
        public ConsoleMenu With(ConsoleColor BackgroundColor, ConsoleColor ForegroundColor)
        {
            this._def_background_color = BackgroundColor;
            this._def_foreground_color = ForegroundColor;

            return this;
        }

        public ConsoleMenu AddMenuItem(string Order, string Text = null, ConsoleColor Color = ConsoleColor.White, Action Action = null)
        {
            this._menuBuilder.Add(new { MenuItemOrder = Order, MenuItemText = Text, MenuItemColor = Color, MenuItemAction = Action });

            return this;
        }

        public void Display()
        {
            this.resetColors();

            Console.Write("**".PadRight(46, '*')); Console.WriteLine("**");
            Console.Write("* ".PadRight(46)); Console.WriteLine(" *");
            foreach (var i in this._menuBuilder)
            {
                if (i.MenuItemOrder == "-")
                {
                    Console.Write("* ".PadRight(46, '-')); 

                }
                else
                {
                    Console.Write("* ");
                    Console.ForegroundColor = i.MenuItemColor;
                    if (i.MenuItemText.Length > 43)
                    {
                        Console.Write(string.Format("{0}. {1}", i.MenuItemOrder, i.MenuItemText).Substring(0,44).PadRight(44));
                    
                    }
                    else
                    {
                        Console.Write(string.Format("{0}. {1}", i.MenuItemOrder, i.MenuItemText).PadRight(44));
                    }
                    
                    
                    

                }
                this.resetColors();
                Console.WriteLine(" *");
            }
            Console.Write("* ".PadRight(46)); Console.WriteLine(" *");
            Console.Write("**".PadRight(46, '*')); Console.WriteLine("**");

            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.Black;
        }

        public ConsoleMenu Run()
        {
            ConsoleKeyInfo cki;

            do
            {
                Console.WriteLine();
                Console.WriteLine("Esc. Exit | M for Menu");
                Console.WriteLine();

                cki = Console.ReadKey(true);

                if (cki.KeyChar.ToString().ToUpper() == "M")
                {
                    this.Display();
                }
                else
                {
                    var r = this._menuBuilder.Find(x => x.MenuItemOrder == cki.KeyChar.ToString().ToUpper());
                    if (null != r && null != r.MenuItemAction) r.MenuItemAction(); Console.WriteLine();

                }

            } while (cki.Key != ConsoleKey.Escape);

            return this;
        }

        private void resetColors()
        {
            Console.BackgroundColor = this._def_background_color;
            Console.ForegroundColor = this._def_foreground_color;
        }
    }
}

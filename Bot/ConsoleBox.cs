using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Data;
using System.Linq;
using System.Text;

namespace Bot
{
    class ConsoleBox
    {
        public string Title { get; set; }
        public int MaxLines { get; set; }
        public int? Top { get; set; }
        public ObservableCollection<Line> Lines { get; set; } = new ObservableCollection<Line>();

        public struct Line
        {
            public string Content { get; set; }
            public ConsoleColor Color { get; set; }
        }

        public ConsoleBox(string title)
        {
            Title = title;
            Lines.CollectionChanged += LinesOnCollectionChanged;
            Top = null;
        }

        public void Update(ConsoleColor color, params Line[] lines)
        {
            var length = Lines.Count;
            for (var i = 0; i < lines.Length; i++)
            {
                if (length < i)
                {
                    Lines.Add(lines[i]);
                }
                else
                {
                    Lines[i] = lines[i];
                }
            }
        }

        public void Update(ConsoleColor color, params string[] lines)
        {
            Update(color, lines.Select(x => new Line
            {
                Content = x,
                Color = color,
            }).ToArray());
        }

        public void Update(params string[] lines)
        {
            Update(ConsoleColor.White, lines);
        }

        private void LinesOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch(e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (Lines.Count >= MaxLines)
                    {
                        Lines.RemoveAt(0);
                    }

                    break;
                case NotifyCollectionChangedAction.Remove:
                    if(Lines.Count >= MaxLines)
                    {
                        Lines.RemoveAt(0);
                    }

                    break;
            }

            if(Top != null)
                Render();
        }

        public static void WriteLine(string line)
        {
            Console.Write(line.Substring(0, Math.Min(Console.WindowWidth, line.Length)));
            if(Console.WindowWidth - line.Length > 0)
                Console.Write(new string(' ', Console.WindowWidth - line.Length));
        }

        private object locker = new object();

        public void Render()
        {
            lock (locker)
            {
                var width = Console.WindowWidth - 5; //Lines.Select(x => x.Content.Length).Max();
                var border = "+-" + new string('-', width) + "-+";
                var titleWidth = Title.Length;

                if (Top == null)
                {
                    Top = Console.CursorTop;
                    MaxLines = Console.WindowHeight - Top.Value;
                }

                Console.CursorTop = Top.Value;
                Console.CursorLeft = 0;

                WriteLine(border.Substring(0, border.Length / 2 - 1) + " " + Title + " " +
                          border.Substring(border.Length / 2 + titleWidth + 1));

                foreach (var line in Lines.ToArray())
                {
                    if (line.Content == null) continue;
                    Console.ForegroundColor = line.Color;
                    WriteLine(" " + line.Content + new string(' ', width - line.Content.Length));
                }
                Console.ResetColor();
            }
        }
    }
}

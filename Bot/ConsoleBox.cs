using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Data;
using System.Linq;
using System.Text;

namespace Bot
{
    /// <summary>
    /// A box in a console ... wait, a console in a box?
    /// </summary>
    class ConsoleBox
    {
        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        /// <value>
        /// The title.
        /// </value>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the maximum lines.
        /// </summary>
        /// <value>
        /// The maximum lines.
        /// </value>
        public int MaxLines { get; set; }

        /// <summary>
        /// Gets or sets the maximum columns.
        /// </summary>
        /// <value>
        /// The maximum columns.
        /// </value>
        public int MaxColumns { get; set; }

        /// <summary>
        /// Gets or sets the top.
        /// </summary>
        /// <value>
        /// The top.
        /// </value>
        public int? Top { get; set; }

        /// <summary>
        /// Gets or sets the left.
        /// </summary>
        /// <value>
        /// The left.
        /// </value>
        public int? Left { get; set; }

        /// <summary>
        /// Gets or sets the lines.
        /// </summary>
        /// <value>
        /// The lines.
        /// </value>
        public ObservableCollection<Line> Lines { get; set; } = new ObservableCollection<Line>();

        /// <summary>
        /// A line in the box
        /// </summary>
        public struct Line
        {
            /// <summary>
            /// Gets or sets the content.
            /// </summary>
            /// <value>
            /// The content.
            /// </value>
            public string Content { get; set; }

            /// <summary>
            /// Gets or sets the color.
            /// </summary>
            /// <value>
            /// The color.
            /// </value>
            public ConsoleColor Color { get; set; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsoleBox"/> class.
        /// </summary>
        /// <param name="title">The title.</param>
        public ConsoleBox(string title)
        {
            Title = title;
            Lines.CollectionChanged += LinesOnCollectionChanged;
            Top = null;
        }

        /// <summary>
        /// Updates the specified color.
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="lines">The lines.</param>
        public void Update(ConsoleColor color, params Line[] lines)
        {
            var length = Lines.Count;
            for (var i = 0; i < lines.Length; i++)
            {
                if (length <= i)
                {
                    Lines.Add(lines[i]);
                }
                else
                {
                    Lines[i] = lines[i];
                }
            }
        }

        /// <summary>
        /// Updates the specified color.
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="lines">The lines.</param>
        public void Update(ConsoleColor color, params string[] lines)
        {
            Update(color, lines.Select(x => new Line
            {
                Content = x,
                Color = color,
            }).ToArray());
        }

        /// <summary>
        /// Updates the specified lines.
        /// </summary>
        /// <param name="lines">The lines.</param>
        public void Update(params string[] lines)
        {
            Update(ConsoleColor.White, lines);
        }

        /// <summary>
        /// Lineses the on collection changed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="NotifyCollectionChangedEventArgs"/> instance containing the event data.</param>
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
                    if(Lines.Count >= MaxLines && Lines.Count > 0)
                    {
                        Lines.RemoveAt(0);
                    }

                    break;
            }

            if(Top != null)
                Render();
        }

        /// <summary>
        /// Writes the line.
        /// </summary>
        /// <param name="line">The line.</param>
        public static void WriteLine(string line)
        {
            Console.Write(line.Substring(0, Math.Min(Console.WindowWidth, line.Length)));
            if(Console.WindowWidth - line.Length > 0)
                Console.Write(new string(' ', Console.WindowWidth - line.Length));
        }

        /// <summary>
        /// The locker
        /// </summary>
        private object locker = new object();

        /// <summary>
        /// Renders this instance.
        /// </summary>
        public void Render()
        {
            lock (locker)
            {
                var titleWidth = Title.Length;

                if (Top == null)
                {
                    Top = Console.CursorTop;
                }

                MaxLines = Console.WindowHeight - Top.Value;

                if (Left == null)
                {
                    Left = 0;
                }

                MaxColumns = Console.WindowWidth;

                var width = MaxColumns - 5;

                if (width < 0) return;
                
                var border = "+-" + new string('-', width) + "-+";

                Console.CursorTop = Top.Value;
                Console.CursorLeft = Left.Value;

                WriteLine(border.Substring(0, border.Length / 2 - 1) + " " + Title + " " +
                          border.Substring(border.Length / 2 + titleWidth + 1));

                foreach (var line in Lines.ToArray())
                {
                    if (line.Content == null) continue;
                    Console.ForegroundColor = line.Color;
                    if(width - line.Content.Length > 0)
                    WriteLine(" " + line.Content + new string(' ', width - line.Content.Length));
                    else
                    {
                        WriteLine(" " + line.Content.Substring(0, Math.Min(line.Content.Count(), width)));
                    }
                }
                Console.ResetColor();
            }
        }
    }
}

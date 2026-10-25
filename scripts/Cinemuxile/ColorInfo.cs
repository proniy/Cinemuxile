using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
namespace Курсач
{
    public class ColorInfo
    {
        public string Name { get; }
        public Color Color { get; }

        public ColorInfo(string name, Color color)
        {
            Name = name;
            Color = color;
        }
    }
}

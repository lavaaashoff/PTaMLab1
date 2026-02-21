using System;
using System.Collections.Generic;
using System.Text;

namespace Form
{
    public enum Type
    {
        Product,
        Node,
        Detail
    }


    public class Element
    {
        //public Element Prev {  get; set; }
        public Element Next { get; set; }
        public string Product { get; set; }
        public Type Type { get; set; }

        public byte ForDelete { get; set; }
    }
}

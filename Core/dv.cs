﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core
{
    public interface dv
    {
        bool eq();
        void rst();
    }

    public class dbool : dv
    {
        public bool eq()
        {
            return s == v;
        }

        public void rst()
        {
            s = v;
        }

        public bool? v;
        private bool? s;
    }

    public class dint : dv
    {
        public dint(int val)
        {
            this.val = val;
            s = null;
        }

        public bool eq()
        {
            return s == val;
        }

        public void rst()
        {
            s = val;
        }

        public int? val;
        private int? s;

        public static implicit operator dint(int v)
        {
            throw new NotImplementedException();
        }

    }

    public class dlong : dv
    {
        public bool eq()
        {
            return s == v;
        }

        public void rst()
        {
            s = v;
        }

        public long? v;
        private long? s;
    }

    public class dfloat : dv
    {
        public bool eq()
        {
            return s == v;
        }

        public void rst()
        {
            s = v;
        }

        public float? v;
        private float? s;
    }

    public class dbyte : dv
    {
        public bool eq()
        {
            return s == v;
        }

        public void rst()
        {
            s = v;
        }

        public byte? v;
        private byte? s;
    }

    public class dchar : dv
    {
        public bool eq()
        {
            return s == v;
        }

        public void rst()
        {
            s = v;
        }

        public char? v;
        private char? s;
    }

    public class ddouble : dv
    {
        public bool eq()
        {
            return s == v;
        }

        public void rst()
        {
            s = v;
        }

        public double? v;
        private double? s;
    }

}

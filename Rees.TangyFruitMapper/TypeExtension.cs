﻿using System;
using System.Reflection;

namespace Rees.TangyFruitMapper
{
    internal static class TypeExtension
    {
        public static bool IsComplexType(this Type instance)
        {
            if (instance.GetTypeInfo().IsPrimitive)
            {
                // https://msdn.microsoft.com/en-us/library/system.type.isprimitive(v=vs.110).aspx
                // Boolean, Byte, SByte, Int16, UInt16, Int32, UInt32, Int64, UInt64, IntPtr, UIntPtr, Char, Double, and Single.
                return false;
            }

            if (instance == typeof(decimal) || instance == typeof(string))
            {
                return false;
            }

            return true;
        }
    }
}

﻿using System.Reflection;

namespace Rees.TangyFruitMapper
{
    internal class DtoMustOnlyHavePublicWriteableProperties : DtoPreconditionRule
    {
        public override void IsCompliant(PropertyInfo property)
        {
            if (!property.CanWrite || !property.SetMethod.IsPublic)
            {
                throw new PropertiesMustBePublicWriteableException();
            }
        }
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Rochas.CacheServer
{
    public static class Reflector
    {
        #region Public Methods

        /// <summary>
        /// Copia os valores das propriedades com identificadores iguais de objetos do mesmo tipo
        /// </summary>
        /// <author>Renato Rocha, 2014</author>
        /// <param name="source">Instância do objeto origem</param>
        /// <param name="destination">Instância do objeto destino</param>
        public static void CloneObjectData(object source, object destination)
        {
            foreach (var prp in getObjectProps(source))
                if (prp.CanWrite)
					getObjectProps(destination, prp.Name)[0].SetValue(destination,
                                                             prp.GetValue(source, null), null);
        }

        /// <summary>
        /// Define o valor da propriedade no objeto com o valor parâmetro informado
        /// </summary>
        /// <author>Renato Rocha, 2014</author>
        /// <param name="source">Instância do objeto</param>
        /// <param name="propName">Nome da propriedade</param>
        /// <param name="value">Valor</param>
        public static void SetObjProp(object source, string propName, object value)
        {
            if (propName != null)
                if (!propName.Contains('.'))
                    setObjPropVal(ref source, propName, value);
                else
                    setChildPropVal(ref source, propName, value);
        }

		/// <summary>
        /// Soma e retorna os valores da propriedade informada dentro dos itens de uma lista
        /// </summary>
        /// <author>Renato Rocha, 2014</author>
        /// <param name="source">Instância da lista de objetos</param>
        /// <param name="sumAttrib">Nome do atributo a somar</param>
        /// <param name="filterAttrib">Nome do atributo a utilizar filtro</param>
        /// <param name="filterValue">Valor do filtro</param>
        public static decimal SumListItems(IEnumerable sourceList, string sumAttrib, string filterAttrib = null, object filterValue = null)
        {
            decimal result = 0;

            if (sourceList != null && !string.IsNullOrEmpty(sumAttrib))
            {
                foreach (var item in sourceList)
                {
                    var itemType = item.GetType();
                    var sumAttribInstance = itemType.GetProperty(sumAttrib);

                    if (sumAttribInstance != null)
                    {
                        var canSum = true;

                        if (!string.IsNullOrEmpty(filterAttrib)
                            && filterValue != null)
                        {
                            var filterAttribInstance = itemType.GetProperty(filterAttrib);
                            if (filterAttribInstance != null)
                                canSum = filterAttribInstance.GetValue(item, null).Equals(filterValue);
                        }

                        if (canSum) result += (decimal)sumAttribInstance.GetValue(item, null);
                    }
                }
            }

            return result;
        }

        public static void ChangeItemsType(IEnumerable objectList, Type objType)
        {
            foreach (var item in objectList)
                Convert.ChangeType(item, objType);
        }

        // Obtem lista genérica do tipo informado
        public static Type GetTypedCollection(Type modelType)
        {
            var engineType = typeof(List<>);
            List<Type> typeTmpList = new List<Type>();
            typeTmpList.Add(modelType);

            return engineType.MakeGenericType(typeTmpList.ToArray());
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Obtem lista filtrada ou não das propriedades de um objeto
        /// </summary>
        /// <author>Renato Rocha, 2014</author>
        /// <param name="source">Instância do objeto</param>
        /// <param name="filter">Lista filtro com identificadores das propriedades a obter</param>
        internal static PropertyInfo[] getObjectProps(object source, params object[] filter)
        {
            List<PropertyInfo> result = new List<PropertyInfo>();
            var objProps = source.GetType().GetProperties();

            if (filter.Length > 0)
                foreach(var flt in filter)
                    if (!flt.ToString().Contains('.'))
                        result = objProps.Where(prp => filter.Contains(prp.Name)).ToList();
                    else
                    {
                        var child = flt.ToString().Split('.');
                        var childSource = source.GetType().GetProperty(child[0]);
                        var childInstance = childSource.GetValue(source, null);
                        if (childInstance == null)
                            childInstance = Activator.CreateInstance(childSource.PropertyType);
                        var childProp = childInstance.GetType().GetProperty(child[1]);
                        if (childProp != null) result.Add(childProp);
                    }
            else
                result = objProps.ToList();

            if (result.Count == 0)
            {
                string strFilters = string.Empty;
                foreach(var flt in filter)
                    strFilters += string.Concat(flt.ToString(), ", ");
                
                throw new Exception(string.Concat("Attribute(s) ", 
                                    strFilters.Substring(0, strFilters.Length - 2), 
                                    " not found in type ", source.GetType().Name));
            }

            return result.ToArray();
        }

        /// <summary>
        /// Obtem lista filtrada ou não das propriedades de um objeto conforme o tipo informado
        /// </summary>
        /// <author>Renato Rocha, 2014</author>
        /// <param name="source">Instância do objeto</param>
        /// <param name="propType">Tipo de propriedade a buscar no objeto</param>
        internal static PropertyInfo[] getObjectProps(object source, Type propType)
        {
            List<PropertyInfo> result = new List<PropertyInfo>();
            var objProps = source.GetType().GetProperties();

            if (propType != null)
                result = objProps.Where(prp => prp.PropertyType.Equals(propType)).ToList();
            else
                result = objProps.ToList();

            return result.ToArray();
        }

        /// <summary>
        /// Obtem o valor do atributo do objeto formatado conforme seu tipo
        /// </summary>
        /// <author>Renato Rocha, 2014</author>
        /// <param name="propValue">Atributo do objeto</param>
        /// <returns>object</returns>
        internal static object getTypedValue(Type propType, object propValue)
        {
            object typedValue = null;

            if (propValue != null)
                if (propValue.GetType().FullName.Contains("DBNull")
                         || propValue.GetType().FullName.Contains("Null"))
                    typedValue = null;
                else if (propType.FullName.Contains("Int16"))
                    typedValue = short.Parse(propValue.ToString());
                else if (propType.FullName.Contains("Int32"))
                    typedValue = int.Parse(propValue.ToString());
                else if (propType.FullName.Contains("Int64"))
                    typedValue = long.Parse(propValue.ToString());
                else if (propType.FullName.Contains("Decimal"))
                    typedValue = decimal.Parse(propValue.ToString());
                else if (propType.FullName.Contains("Float"))
                    typedValue = float.Parse(propValue.ToString());
                else if (propType.FullName.Contains("Double"))
                    typedValue = double.Parse(propValue.ToString());
                else if (propValue.GetType().FullName.Contains("String"))
                    typedValue = propValue.ToString();
                else if (propValue.GetType().FullName.Contains("DateTime"))
                {
                    if (propValue.ToString().Contains("00:00:00"))
                        typedValue = propValue.ToString().Replace("00:00:00", string.Empty);
                    
                    typedValue = DateTime.Parse(propValue.ToString());
                }
                else
                    typedValue = propValue;

            return typedValue;
        }

        /// <summary>
        /// Define o valor da propriedade no objeto com o valor parâmetro informado
        /// </summary>
        /// <author>Renato Rocha, 2014</author>
        /// <param name="source">Instância do objeto</param>
        /// <param name="propName">Nome da propriedade</param>
        /// <param name="value">Valor</param>
        internal static void setObjPropVal(ref object source, string propName, object value)
        {
            var prop = source.GetType().GetProperty(propName);
            
            if (prop != null)
                prop.SetValue(source, getTypedValue(prop.PropertyType, value), null);
        }

        /// <summary>
        /// Define o valor da propriedade de um objeto filho com o valor parâmetro informado
        /// </summary>
        /// <author>Renato Rocha, 2014</author>
        /// <param name="source">Instância do objeto</param>
        /// <param name="propName">Nome da propriedade</param>
        /// <param name="value">Valor</param>
        internal static void setChildPropVal(ref object source, string propName, object value)
        {
            var childArr = propName.Split('.');
            var childObjName = childArr[0];
            var childPropName = childArr[1];
            var childType = getObjectProps(source, childObjName)
                           .FirstOrDefault().PropertyType;

            var childInst = getObjectProps(source, childObjName).FirstOrDefault().GetValue(source, null);
            if (childInst == null) childInst = Activator.CreateInstance(childType);

            setObjPropVal(ref childInst, childPropName, value ?? new object());
            setObjPropVal(ref source, childObjName, childInst);
        }

        #endregion
    }

    public static class Reflector<T>
    {
        #region Public Methods

        /// <summary>
        /// Define os valores das propriedades em uma nova instância do objeto origem
        /// </summary>
        /// <author>Renato Rocha, 2014</author>
        /// <param name="source">Instância do objeto</param>
        public static T CloneObjectData(object source)
        {
            T destination = Activator.CreateInstance<T>();

            foreach (var prp in Reflector.getObjectProps(source))
                Reflector.getObjectProps(destination, prp.Name)[0].SetValue(destination,
                                                                   prp.GetValue(source, null), null);

            return destination;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Define os valores das propriedades do objeto origem para a destino
        /// </summary>
        /// <author>Renato Rocha, 2014</author>
        /// <param name="source">Instância do objeto</param>
        /// <param name="properties">Lista com as propriedades do objeto a preencher</param>
        internal static T setObjectValues(object source, PropertyInfo[] properties)
        {
            T destination = Activator.CreateInstance<T>();

            foreach (var prp in properties)
                Reflector.SetObjProp(source, prp.Name,
                                     Reflector.getObjectProps(source, prp.Name).FirstOrDefault().GetValue(source, null));

            return destination;
        }

        #endregion
    }
}

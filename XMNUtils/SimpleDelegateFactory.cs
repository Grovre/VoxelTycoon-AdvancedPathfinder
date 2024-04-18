namespace XMNUtils
{
// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DelegateFactory_FieldGet.cs" company="Natan Podbielski">
//   Copyright (c) 2016 - 2018 Natan Podbielski. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
//using Delegates.CustomDelegates;
//using Delegates.Extensions;
//using static Delegates.Helper.DelegateHelper;

    /// <summary>
    ///     Creates delegates for types members
    /// </summary>
    public static partial class SimpleDelegateFactory
    {
        /// <summary>
        ///     Creates delegate for retrieving instance field value
        /// </summary>
        /// <typeparam name="TSource">Source type with defined field</typeparam>
        /// <typeparam name="TField">Type of field</typeparam>
        /// <param name="fieldName">Field name</param>
        /// <returns>Delegate for retrieving instance field value</returns>
        public static Func<TSource, TField>
            FieldGet<TSource, TField>(string fieldName)
        {
            return typeof(TSource).FieldGetImpl<Func<TSource, TField>>(fieldName);
        }

        /// <summary>
        ///     Creates delegate for retrieving instance field value
        /// </summary>
        /// <typeparam name="TField">Type of field</typeparam>
        /// <param name="source">Type with defined field</param>
        /// <param name="fieldName">Field name</param>
        /// <returns>Delegate for retrieving instance field value</returns>
        public static Func<object, TField> FieldGet<TField>(this Type source,
            string fieldName)
        {
            return source.FieldGetImpl<Func<object, TField>>(fieldName);
        }

        /// <summary>
        ///     Creates delegate for retrieving instance field value as object from source instance as object
        /// </summary>
        /// <param name="source">Type with defined field</param>
        /// <param name="fieldName">Field name</param>
        /// <returns>Delegate for retrieving instance field value</returns>
        public static Func<object, object> FieldGet(this Type source, string fieldName)
        {
            return source.FieldGetImpl<Func<object, object>>(fieldName);
        }

        private static TDelegate FieldGetImpl<TDelegate>(this Type source, string fieldName, bool byRef = false)
            where TDelegate : class
        {
            var fieldInfo = source.GetFieldInfo(fieldName, false);
            if (fieldInfo != null)
            {
                var sourceTypeInDelegate = GetDelegateArguments<TDelegate>().First();
                Expression instanceExpression;
                ParameterExpression sourceParam;
                if (sourceTypeInDelegate.IsByRef == false
                    ? sourceTypeInDelegate != source
                    : sourceTypeInDelegate.GetElementType() != source)
                {
                    sourceParam = Expression.Parameter(typeof(object), "source");
                    instanceExpression = Expression.Convert(sourceParam, source);
                }
                else
                {
                    if (byRef && source.GetTypeInfo().IsValueType)
                    {
                        sourceParam = Expression.Parameter(source.MakeByRefType(), "source");
                        instanceExpression = sourceParam;
                    }
                    else
                    {
                        sourceParam = Expression.Parameter(source, "source");
                        instanceExpression = sourceParam;
                    }
                }

                Expression returnExpression = Expression.Field(instanceExpression, fieldInfo);
                if (!fieldInfo.FieldType.GetTypeInfo().IsClass)
                    returnExpression = Expression.Convert(returnExpression, GetDelegateReturnType<TDelegate>());
                var lambda = Expression.Lambda<TDelegate>(returnExpression, sourceParam);
                var fieldGetImpl = lambda.Compile();
                return fieldGetImpl;
            }

            return null;
        }
        /// <summary>
        ///     Creates delegate for setting instance field value in instance by passed by object
        /// </summary>
        /// <typeparam name="TField">Type of field</typeparam>
        /// <param name="source">Type with defined field</param>
        /// <param name="fieldName">Field name</param>
        /// <returns>Delegate for setting instance field value</returns>
        public static Action<object, TField> FieldSet<TField>(this Type source, string fieldName)
        {
            var fieldInfo = source.GetFieldInfo(fieldName, false);
            if (fieldInfo != null && !fieldInfo.IsInitOnly)
            {
                var sourceParam = Expression.Parameter(typeof(object), "source");
                Expression valueExpr;
                var valueParam = Expression.Parameter(typeof(TField), "value");
                if (fieldInfo.FieldType == typeof(TField))
                    valueExpr = valueParam;
                else
                    valueExpr = Expression.Convert(valueParam, fieldInfo.FieldType);
                var lambda = Expression.Lambda<Action<object, TField>>(
                    Expression.Assign(Expression.Field(Expression.Convert(sourceParam, source), fieldInfo), valueExpr),
                    sourceParam, valueParam);
                return lambda.Compile();
            }

            return null;
        }
        /// <summary>
        ///     Creates delegate for setting instance field value in instance
        /// </summary>
        /// <typeparam name="TSource">Source type with defined field</typeparam>
        /// <typeparam name="TField">Type of field</typeparam>
        /// <param name="fieldName">Field name</param>
        /// <returns>Delegate for setting instance field value</returns>
        public static Action<TSource, TField> FieldSet<TSource, TField>(string fieldName)
            where TSource : class
        {
            var source = typeof(TSource);
            var fieldInfo = source.GetFieldInfo(fieldName, false);
            if (fieldInfo != null && !fieldInfo.IsInitOnly)
            {
                var sourceParam = Expression.Parameter(source, "source");
                var valueParam = Expression.Parameter(typeof(TField), "value");
                var lambda = Expression.Lambda<Action<TSource, TField>>(
                    Expression.Assign(Expression.Field(sourceParam, fieldInfo), valueParam),
                    sourceParam, valueParam);
                return lambda.Compile();
            }

            return null;
        }

        /// <summary>
        ///     Creates delegate for setting instance field value as object in instance as object
        /// </summary>
        /// <param name="source">Type with defined field</param>
        /// <param name="fieldName">Field name</param>
        /// <returns>Delegate for setting instance field value</returns>
        public static Action<object, object> FieldSet(this Type source, string fieldName)
        {
            var fieldInfo = source.GetFieldInfo(fieldName, false);
            if (fieldInfo != null && !fieldInfo.IsInitOnly)
            {
                var sourceParam = Expression.Parameter(typeof(object), "source");
                var valueParam = Expression.Parameter(typeof(object), "value");
                var convertedValueExpr = Expression.Convert(valueParam, fieldInfo.FieldType);
                Expression returnExpression =
                    Expression.Assign(Expression.Field(Expression.Convert(sourceParam, source), fieldInfo),
                        convertedValueExpr);
                if (!fieldInfo.FieldType.GetTypeInfo().IsClass)
                    returnExpression = Expression.Convert(returnExpression, typeof(object));
                var lambda = Expression.Lambda<Action<object, object>>(returnExpression, sourceParam, valueParam);
                return lambda.Compile();
            }

            return null;
        }
        public static FieldInfo GetFieldInfo(this Type source, string fieldName, bool isStatic)
        {
            var staticOrInstance = isStatic ? BindingFlags.Static : BindingFlags.Instance;
            var typeInfo = source.GetTypeInfo();
            var fieldInfo = (typeInfo.GetField(fieldName, staticOrInstance) ??
                             typeInfo.GetField(fieldName, staticOrInstance | BindingFlags.NonPublic)) ??
                            typeInfo.GetField(fieldName, staticOrInstance | BindingFlags.Public | BindingFlags.NonPublic);
            return fieldInfo;
        }

        public static Type[] GetDelegateArguments<TDelegate>() where TDelegate : class
        {
            var delegateType = typeof(TDelegate);
            var invokeMethod = CheckInvokeMethod(delegateType);
            return invokeMethod.GetParameters().Select(p => p.ParameterType).ToArray();
        }

        private static MethodInfo CheckInvokeMethod(Type delegateType)
        {
            var invokeMethod = delegateType.GetMethod("Invoke");
            if (invokeMethod == null)
                throw new ArgumentException(
                    $"TDelegate type do not have Invoke method. Check if delegate base class is {typeof(Delegate).FullName}.");
            return invokeMethod;
        }

        public static Type GetDelegateReturnType<TDelegate>() where TDelegate : class
        {
            var delegateType = typeof(TDelegate);
            var invokeMethod = CheckInvokeMethod(delegateType);
            return invokeMethod.ReturnType;
        }
    }
}
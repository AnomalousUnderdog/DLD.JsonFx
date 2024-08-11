#region License

/*---------------------------------------------------------------------------------*\

	Distributed under the terms of an MIT-style license:

	The MIT License

	Copyright (c) 2006-2009 Stephen M. McKamey

	Permission is hereby granted, free of charge, to any person obtaining a copy
	of this software and associated documentation files (the "Software"), to deal
	in the Software without restriction, including without limitation the rights
	to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
	copies of the Software, and to permit persons to whom the Software is
	furnished to do so, subject to the following conditions:

	The above copyright notice and this permission notice shall be included in
	all copies or substantial portions of the Software.

	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
	IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
	FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
	AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
	LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
	OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
	THE SOFTWARE.

\*---------------------------------------------------------------------------------*/

#endregion License

#if WINDOWS_STORE
using TP = System.Reflection.TypeInfo;
#else
using TP = System.Type;
#endif
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace DLD.JsonFx
{
	public interface ISerializationRule
	{
		SerializationRuleType ShouldBeSerialized { get; }
	}


	/// <summary>
	/// Utility for forcing conversion between types
	/// </summary>
	internal class TypeCoercionUtility
	{
		#region Constants

		const string ErrorNullValueType = "{0} does not accept null as a value";
		const string ErrorDefaultCtor = "Only objects with default constructors can be deserialized. ({0})";
		const string ErrorCannotInstantiate = "Interfaces, Abstract classes, and unsupported ValueTypes cannot be deserialized. ({0})";

		#endregion Constants

		#region Fields

		ConcurrentDictionary<TP, Dictionary<string, MemberInfo>> _memberMapCache;
		bool _allowNullValueTypes = true;

		readonly ConcurrentDictionary<string, TP> _hintedTypeCache = new ConcurrentDictionary<string, TP>();

		SerializationRuleType _shouldBeSerialized;

		public void SetFieldSerializationRule(SerializationRuleType newVal)
		{
			_shouldBeSerialized = newVal;
		}

		SerializedNameType _serializedName;
		public void SetFieldSerializedName(SerializedNameType newVal)
		{
			_serializedName = newVal;
		}

		#endregion Fields

		#region Properties

		public static TP GetTypeInfo(TP tp)
		{
#if WINDOWS_STORE
			return tp.GetTypeInfo ();
#else
			return tp;
#endif
		}

		IDictionary<TP, Dictionary<string, MemberInfo>> MemberMapCache
		{
			get
			{
				if (_memberMapCache == null)
				{
					// instantiate space for cache
					_memberMapCache = new ConcurrentDictionary<Type, Dictionary<string, MemberInfo>>();
				}

				return _memberMapCache;
			}
		}

		/// <summary>
		/// Gets and sets if ValueTypes can accept values of null
		/// </summary>
		/// <remarks>
		/// Only affects deserialization: if a ValueType is assigned the
		/// value of null, it will receive the value default(TheType).
		/// Setting this to false, throws an exception if null is
		/// specified for a ValueType member.
		/// </remarks>
		public bool AllowNullValueTypes
		{
			get => _allowNullValueTypes;
			set => _allowNullValueTypes = value;
		}

		#endregion Properties

		#region Object Methods

		/// <summary>
		/// If a Type Hint is present then this method attempts to
		/// use it and move any previously parsed data over.
		/// </summary>
		/// <param name="result">the previous result</param>
		/// <param name="typeInfo">the type info string to use</param>
		/// <param name="assemblyNamesToSearchThroughIfNotFound"></param>
		/// <param name="searchThroughAllAssembliesIfNotFound"></param>
		/// <param name="objectType">reference to the objectType</param>
		/// <param name="memberMap">reference to the memberMap</param>
		/// <returns></returns>
		internal object ProcessTypeHint(IDictionary result, string typeInfo,
			IReadOnlyList<string> assemblyNamesToSearchThroughIfNotFound, bool searchThroughAllAssembliesIfNotFound,
			ref TP objectType, ref Dictionary<string, MemberInfo> memberMap)
		{
			//Debug.Log(string.Format("ProcessTypeHint: typeInfo is {0}", typeInfo));
			if (string.IsNullOrEmpty(typeInfo))
			{
				//Debug.Log("ProcessTypeHint: typeInfo is null");
				objectType = null;
				memberMap = null;
				return result;
			}

			Type hintedType = GetType(typeInfo, assemblyNamesToSearchThroughIfNotFound,
				searchThroughAllAssembliesIfNotFound);

			if (Equals(hintedType, null))
			{
				// The commented code below nullifies the constructed data so far
				// if the type hint is invalid (referring to a non-existent type/namespace/assembly).
				// I commented that out so that the code can still
				// deserialize a class that was renamed, for example,
				// or if the object being deserialized was moved to a
				// different assembly or namespace, etc.
				//objectType = null;
				//memberMap = null;

				return result;
			}

			objectType = hintedType;
			return CoerceType(hintedType, result, out memberMap);
		}

		internal object ProcessTypeHint(object result, string typeInfo,
			IReadOnlyList<string> assemblyNamesToSearchThroughIfNotFound, bool searchThroughAllAssembliesIfNotFound,
			ref TP objectType, ref Dictionary<string, MemberInfo> memberMap)
		{
			Type hintedType = GetType(typeInfo, assemblyNamesToSearchThroughIfNotFound,
				searchThroughAllAssembliesIfNotFound);

			if (Equals(hintedType, null))
			{
				// The commented code below nullifies the constructed data so far
				// if the type hint is invalid (referring to a non-existent type/namespace/assembly).
				// I commented that out so that the code can still
				// deserialize a class that was renamed, for example,
				// or if the object being deserialized was moved to a
				// different assembly or namespace, etc.
				//objectType = null;
				//memberMap = null;

				return result;
			}

			objectType = hintedType;

			if (result != null && result.GetType() == hintedType)
			{
				memberMap = GetMemberMap(objectType);
				return result;
			}

			// result is null, need to instantiate instance for it first
			return InstantiateObject(objectType, out memberMap);
		}

		TP GetType(string typeInfo, IReadOnlyList<string> assemblyNamesToSearchThroughIfNotFound, bool searchThroughAllAssembliesIfNotFound)
		{
			if (string.IsNullOrWhiteSpace(typeInfo))
			{
				return null;
			}

			if (_hintedTypeCache.TryGetValue(typeInfo, out Type hintedType))
			{
				return hintedType;
			}

			hintedType = Type.GetType(typeInfo, false);

			if (hintedType != null)
			{
				_hintedTypeCache[typeInfo] = hintedType;
				return hintedType;
			}

			// ---------------------------------------------------------
			// Type.GetType failed.
			// Try with other assemblies.
			// Remove the assembly name specified in the string,
			// then try the assembly names specified.

			int typeCommaIdx = typeInfo.IndexOf(",", StringComparison.Ordinal);
			string typeName = typeCommaIdx != -1 ? typeInfo.Substring(0, typeCommaIdx) : typeInfo;

			if (assemblyNamesToSearchThroughIfNotFound != null)
			{
				for (int i = 0; i < assemblyNamesToSearchThroughIfNotFound.Count; i++)
				{
					string tryTypeInfo = $"{typeName}, {assemblyNamesToSearchThroughIfNotFound[i]}";
					hintedType = Type.GetType(tryTypeInfo, false);
					if (hintedType != null)
					{
						_hintedTypeCache[typeInfo] = hintedType;
						return hintedType;
					}
				}
			}

			if (searchThroughAllAssembliesIfNotFound)
			{
				var assemblies = AppDomain.CurrentDomain.GetAssemblies();
				for (int i = 0; i < assemblies.Length; i++)
				{
					string tryTypeInfo = $"{typeName}, {assemblies[i].GetName().Name}";
					hintedType = Type.GetType(tryTypeInfo, false);
					if (hintedType != null)
					{
						_hintedTypeCache[typeInfo] = hintedType;
						return hintedType;
					}
				}
			}

			return null;
		}

		internal object InstantiateObject(TP objectType)
		{
			/*if (TCU.GetTypeInfo(objectType).IsInterface || TCU.GetTypeInfo(objectType).IsAbstract || TCU.GetTypeInfo(objectType).IsValueType)
			{
				throw new JsonTypeCoercionException(
					String.Format(TypeCoercionUtility.ErrorCannotInstantiate, new System.Object[] {objectType.FullName}));
			}

			ConstructorInfo ctor = objectType.GetConstructor(Type.EmptyTypes);
			if (ConstructorInfo.Equals (ctor, null)) {
				throw new JsonTypeCoercionException (
					String.Format (TypeCoercionUtility.ErrorDefaultCtor, new System.Object[] { objectType.FullName }));
			}
			Object result;
			try
			{
				// always try-catch Invoke() to expose real exception
				result = ctor.Invoke(null);
			}
			catch (TargetInvocationException ex)
			{
				if (ex.InnerException != null)
				{
					throw new JsonTypeCoercionException(ex.InnerException.Message, ex.InnerException);
				}
				throw new JsonTypeCoercionException("Error instantiating " + objectType.FullName, ex);
			}*/
			return Activator.CreateInstance(objectType);
			//return result;
		}

		internal object InstantiateObject(TP objectType, out Dictionary<string, MemberInfo> memberMap)
		{
			object o = InstantiateObject(objectType);
			memberMap = GetMemberMap(objectType);

			return o;
		}

		/// <summary>
		/// Dictionary from types to a list of (string,FieldInfo) pairs and a list of (string,PropertyInfo) pairs
		/// </summary>
		Dictionary<TP, KeyValuePair<KeyValuePair<string, FieldInfo>[], KeyValuePair<string, PropertyInfo>[]>>
			_writingMaps;

		List<KeyValuePair<string, FieldInfo>> _fieldList;
		List<KeyValuePair<string, PropertyInfo>> _propList;

		public void GetMemberWritingMap(TP objectType, JsonWriterSettings settings,
			out KeyValuePair<string, FieldInfo>[] outFields, out KeyValuePair<string, PropertyInfo>[] outProps)
		{
			if (_writingMaps == null)
			{
				_writingMaps =
					new Dictionary<Type,
						KeyValuePair<KeyValuePair<string, FieldInfo>[], KeyValuePair<string, PropertyInfo>[]>>();
			}

			KeyValuePair<KeyValuePair<string, FieldInfo>[], KeyValuePair<string, PropertyInfo>[]> pair;
			if (_writingMaps.TryGetValue(objectType, out pair))
			{
#if JSONFX_DEBUG
				Debug.Log($"For {objectType.FullName}, reusing existing WritingMap. Fields: {pair.Key.Length} Properties: {pair.Value.Length}");
#endif
				outFields = pair.Key;
				outProps = pair.Value;
				return;
			}

			bool anonymousType = objectType.IsGenericType && objectType.Name.StartsWith(JsonWriter.AnonymousTypePrefix);


			if (_fieldList == null)
				_fieldList = new List<KeyValuePair<string, FieldInfo>>();

			if (_propList == null)
				_propList = new List<KeyValuePair<string, PropertyInfo>>();

			_fieldList.Clear();
			_propList.Clear();


			List<Type> typeChain = new List<Type>();

			Type tp = objectType;
			while (tp != null)
			{
				typeChain.Add(tp);
				tp = tp.BaseType;
			}

#if JSONFX_DEBUG
			var sb = new System.Text.StringBuilder();
			sb.Append("Creating new WritingMap for ");
			sb.AppendLine(objectType.FullName);
#endif

			// iterate through the inheritance chain in reverse so that we start at the base type
			for (int tpIdx = typeChain.Count - 1; tpIdx >= 0; --tpIdx)
			{
				FieldInfo[] fields = typeChain[tpIdx].GetFields(BindingFlags.Instance |
				                                                BindingFlags.Public |
				                                                BindingFlags.NonPublic |
				                                                BindingFlags.DeclaredOnly);

#if JSONFX_DEBUG
				sb.Append("Fields in ");
				sb.Append(typeChain[tpIdx].Name);
				sb.Append(": ");
				sb.Append(fields.Length);
				sb.AppendLine();
#endif
				for (int j = 0; j < fields.Length; j++)
				{
					FieldInfo field = fields[j];

#if JSONFX_DEBUG
					sb.Append(j+1);
					sb.Append(". ");
					sb.Append(field.Name);
					sb.Append(":");
#endif
					if (_shouldBeSerialized != null)
					{
						if (!_shouldBeSerialized(field))
						{
#if JSONFX_DEBUG
							sb.AppendLine(" Cannot serialize, because of SerializationRule");
#endif
							continue;
						}
#if JSONFX_DEBUG
						sb.Append(" Will serialize, because of SerializationRule.");
#endif
					}
					else if (field.IsStatic ||
					         (!field.IsPublic && field.GetCustomAttributes(typeof(JsonMemberAttribute), true).Length == 0))
					{
#if JSONFX_DEBUG
						sb.AppendLine(" Cannot serialize, not public or is static (and does not have a JsonMember attribute)");
#endif
						continue;
					}

					if (settings.IsIgnored(objectType, field, null))
					{
#if JSONFX_DEBUG
						sb.AppendLine(" Cannot serialize, ignored by settings");
#endif
						continue;
					}

					string fieldName = JsonNameAttribute.GetJsonName(field, _serializedName);
					if (string.IsNullOrEmpty(fieldName))
						fieldName = field.Name;

#if JSONFX_DEBUG
					sb.Append(" Serialized as \"");
					sb.Append(fieldName);
					sb.AppendLine("\"");
#endif

					_fieldList.Add(new KeyValuePair<string, FieldInfo>(fieldName, field));
				}

				PropertyInfo[] properties = typeChain[tpIdx].GetProperties(BindingFlags.Instance |
				                                                           BindingFlags.Public |
				                                                           BindingFlags.NonPublic |
				                                                           BindingFlags.DeclaredOnly);

#if JSONFX_DEBUG
				sb.Append("Properties in ");
				sb.Append(typeChain[tpIdx].Name);
				sb.Append(": ");
				sb.Append(properties.Length);
				sb.AppendLine();
#endif
				for (int j = 0; j < properties.Length; j++)
				{
					PropertyInfo property = properties[j];

#if JSONFX_DEBUG
					sb.Append(j+1);
					sb.Append(". ");
					sb.Append(property.Name);
					sb.Append(":");
#endif
					if (!property.CanRead)
					{
#if JSONFX_DEBUG
						sb.AppendLine(" Cannot serialize, cannot read");
#endif
						continue;
					}

					if (!property.CanWrite && !anonymousType)
					{
#if JSONFX_DEBUG
						sb.AppendLine(" Cannot serialize, cannot write");
#endif
						continue;
					}

					if (_shouldBeSerialized != null)
					{
						if (!_shouldBeSerialized(property))
						{
#if JSONFX_DEBUG
							sb.AppendLine(" Cannot serialize, because of SerializationRule");
#endif
							continue;
						}
#if JSONFX_DEBUG
						sb.Append(" Will serialize, because of SerializationRule.");
#endif
					}

					if (settings.IsIgnored(objectType, property, null))
					{
#if JSONFX_DEBUG
						sb.AppendLine(" Cannot serialize, is ignored by settings");
#endif
						continue;
					}

					if (property.GetIndexParameters().Length != 0)
					{
#if JSONFX_DEBUG
						sb.AppendLine(" Cannot serialize, is indexed");
#endif
						continue;
					}

					// use Attributes here to control naming
					string propertyName = JsonNameAttribute.GetJsonName(property, _serializedName);
					if (string.IsNullOrEmpty(propertyName))
						propertyName = property.Name;

#if JSONFX_DEBUG
					sb.Append(" Serialized as \"");
					sb.Append(propertyName);
					sb.AppendLine("\"");
#endif

					_propList.Add(new KeyValuePair<string, PropertyInfo>(propertyName, property));
				}
			}

			outFields = _fieldList.ToArray();
			outProps = _propList.ToArray();

			pair = new KeyValuePair<KeyValuePair<string, FieldInfo>[], KeyValuePair<string, PropertyInfo>[]>(outFields,
				outProps);

#if JSONFX_DEBUG
			sb.Append("Fields: ");
			sb.Append(outFields.Length);
			sb.Append(", ");
			sb.Append("Properties: ");
			sb.Append(outProps.Length);
			sb.AppendLine();
			Debug.Log(sb.ToString());
#endif

			_writingMaps[objectType] = pair;
		}

		/// <summary>
		/// Returns a member map if suitable for the object type.
		/// Dictionary types will make this method return null.
		/// </summary>
		/// <param name="objectType"></param>
		/// <returns></returns>
		public Dictionary<string, MemberInfo> GetMemberMap(TP objectType)
		{
			// don't incur the cost of member map for dictionaries
			if (GetTypeInfo(typeof(IDictionary)).IsAssignableFrom(GetTypeInfo(objectType)))
			{
				return null;
			}

			return CreateMemberMap(objectType);
		}

		/// <summary>
		/// Creates a member map for the type
		/// </summary>
		/// <param name="objectType"></param>
		/// <returns></returns>
		Dictionary<string, MemberInfo> CreateMemberMap(TP objectType)
		{
			Dictionary<string, MemberInfo> memberMap;

			if (MemberMapCache.TryGetValue(objectType, out memberMap))
			{
#if JSONFX_DEBUG
				Debug.Log($"For {objectType.FullName}, reusing existing MemberMap. Members: {memberMap.Count}");
#endif
				// map was stored in cache
				return memberMap;
			}

			// create a new map
			memberMap = new Dictionary<string, MemberInfo>();


#if JSONFX_DEBUG
			var sb = new System.Text.StringBuilder();
			sb.Append("Creating new MemberMap for ");
			sb.AppendLine(objectType.FullName);
#endif

			// load properties into property map
			Type tp = objectType;
			while (tp != null)
			{
#if JSONFX_DEBUG
				sb.Append("..going inside ");
				sb.AppendLine(tp.Name);
#endif

				PropertyInfo[] properties = GetTypeInfo(tp)
					.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

#if JSONFX_DEBUG
				sb.Append("Properties in ");
				sb.Append(tp.Name);
				sb.Append(": ");
				sb.Append(properties.Length);
				sb.AppendLine();
#endif
				for (int i = 0; i < properties.Length; i++)
				{
					PropertyInfo info = properties[i];

#if JSONFX_DEBUG
					sb.Append(i+1);
					sb.Append(". ");
					sb.Append(info.Name);
					sb.Append(":");
#endif

					if (!info.CanRead || !info.CanWrite)
					{
#if JSONFX_DEBUG
						sb.AppendLine(" Skipping property, can't read/write");
#endif
						continue;
					}

					if (_shouldBeSerialized != null)
					{
						if (!_shouldBeSerialized(info))
						{
#if JSONFX_DEBUG
							sb.AppendLine(" Not deserializing, because of SerializationRule");
#endif
							continue;
						}
#if JSONFX_DEBUG
						sb.Append(" Will deserialize, because of SerializationRule.");
#endif
					}

					if (JsonIgnoreAttribute.IsJsonIgnore(info))
					{
						continue;
					}

					string jsonName = JsonNameAttribute.GetJsonName(info, _serializedName);

#if JSONFX_DEBUG
					sb.Append($" Will deserialize property as \"{jsonName}\"");
					sb.AppendLine();
#endif

					if (string.IsNullOrEmpty(jsonName))
					{
						memberMap[info.Name] = info;
					}
					else
					{
						memberMap[jsonName] = info;
					}
				}

				// load public fields into property map
				FieldInfo[] fields = GetTypeInfo(tp).GetFields(BindingFlags.NonPublic |
				                                               BindingFlags.Public |
				                                               BindingFlags.Instance |
				                                               BindingFlags.DeclaredOnly);

#if JSONFX_DEBUG
				sb.Append("Fields in ");
				sb.Append(tp.Name);
				sb.Append(": ");
				sb.Append(fields.Length);
				sb.AppendLine();
#endif
				for (int i = 0; i < fields.Length; i++)
				{
					FieldInfo info = fields[i];
#if JSONFX_DEBUG
					sb.Append(i+1);
					sb.Append(". ");
					sb.Append(info.Name);
					sb.Append(":");
#endif

					if (_shouldBeSerialized != null)
					{
						if (!_shouldBeSerialized(info))
						{
#if JSONFX_DEBUG
							sb.AppendLine(" Not deserializing, because of SerializationRule");
#endif
							continue;
						}
#if JSONFX_DEBUG
						sb.Append(" Will deserialize, because of SerializationRule.");
#endif
					}
					else if (!info.IsPublic &&
#if WINDOWS_STORE
						info.GetCustomAttribute<JsonMemberAttribute>(false) == null
#else
					         info.GetCustomAttributes(typeof(JsonMemberAttribute), false).Length == 0
#endif
					        )
					{
						continue;
					}

					if (JsonIgnoreAttribute.IsJsonIgnore(info))
					{
						continue;
					}

					string jsonName = JsonNameAttribute.GetJsonName(info, _serializedName);

#if JSONFX_DEBUG
					sb.Append($" Will deserialize field as \"{jsonName}\"");
					sb.AppendLine();
#endif

					if (string.IsNullOrEmpty(jsonName))
					{
						memberMap[info.Name] = info;
					}
					else
					{
						memberMap[jsonName] = info;
					}
				}

				tp = tp.BaseType;
			}

#if JSONFX_DEBUG
			sb.Append("Members: ");
			sb.Append(memberMap.Count);
			sb.AppendLine();
			Debug.Log(sb.ToString());
#endif

			// store in cache for repeated usage
			MemberMapCache[objectType] = memberMap;

			return memberMap;
		}

		internal static TP GetMemberInfo(Dictionary<string, MemberInfo> memberMap,
			string memberName,
			out MemberInfo memberInfo)
		{
			if (memberMap != null &&
			    memberMap.TryGetValue(memberName, out memberInfo))
			{
				// Check properties for object member
				//memberInfo = memberMap[memberName];

				if (memberInfo is PropertyInfo)
				{
					// maps to public property
					return ((PropertyInfo)memberInfo).PropertyType;
				}

				if (memberInfo is FieldInfo)
				{
					// maps to public field
					return ((FieldInfo)memberInfo).FieldType;
				}
			}

			memberInfo = null;
			return null;
		}

		/// <summary>
		/// Helper method to set value of either property or field
		/// </summary>
		/// <param name="result"></param>
		/// <param name="memberType"></param>
		/// <param name="memberInfo"></param>
		/// <param name="value"></param>
		internal void SetMemberValue(object result, TP memberType, MemberInfo memberInfo, object value)
		{
			if (memberInfo is PropertyInfo)
			{
				// set value of public property
				((PropertyInfo)memberInfo).SetValue(result,
					CoerceType(memberType, value),
					null);
			}
			else if (memberInfo is FieldInfo)
			{
				// set value of public field
				((FieldInfo)memberInfo).SetValue(result,
					CoerceType(memberType, value));
			}

			// all other values are ignored
		}

		#endregion Object Methods

		#region Type Methods

		internal object CoerceType(TP targetType, object value)
		{
			bool isNullable = IsNullable(targetType);
			if (value == null)
			{
				if (!_allowNullValueTypes &&
				    GetTypeInfo(targetType).IsValueType &&
				    !isNullable)
				{
					throw new JsonTypeCoercionException(string.Format(ErrorNullValueType,
						new object[] { targetType.FullName }));
				}

				return null;
			}

			if (isNullable)
			{
				// nullable types have a real underlying struct
				Type[] genericArgs = targetType.GetGenericArguments();
				if (genericArgs.Length == 1)
				{
					targetType = genericArgs[0];
				}
			}

			Type actualType = value.GetType();
			if (GetTypeInfo(targetType).IsAssignableFrom(GetTypeInfo(actualType)))
			{
				return value;
			}

			if (GetTypeInfo(targetType).IsEnum)
			{
				if (value is string)
				{
					if (!Enum.IsDefined(targetType, value))
					{
						// if isn't a defined value perhaps it is the JsonName
						foreach (FieldInfo field in GetTypeInfo(targetType).GetFields())
						{
							string jsonName = JsonNameAttribute.GetJsonName(field, _serializedName);
							if (((string)value).Equals(jsonName))
							{
								value = field.Name;
								break;
							}
						}
					}

					return Enum.Parse(targetType, (string)value);
				}

				value = CoerceType(Enum.GetUnderlyingType(targetType), value);
				return Enum.ToObject(targetType, value);
			}

			if (value is IDictionary dictionary)
			{
				Dictionary<string, MemberInfo> memberMap;
				return CoerceType(targetType, dictionary, out memberMap);
			}

			if (GetTypeInfo(typeof(IEnumerable)).IsAssignableFrom(GetTypeInfo(targetType)) &&
			    GetTypeInfo(typeof(IEnumerable)).IsAssignableFrom(GetTypeInfo(actualType)))
			{
				return CoerceList(targetType, actualType, (IEnumerable)value);
			}

			if (value is string stringValue)
			{
				if (targetType == typeof(DateTime))
				{
					if (DateTime.TryParse(stringValue,
						    DateTimeFormatInfo.InvariantInfo,
						    DateTimeStyles.RoundtripKind |
						    DateTimeStyles.AllowWhiteSpaces |
						    DateTimeStyles.NoCurrentDateDefault,
						    out DateTime date))
					{
						return date;
					}
				}
				else if (targetType == typeof(Guid))
				{
					// try-catch is pointless since will throw upon generic conversion
					return new Guid(stringValue);
				}
				else if (targetType == typeof(char))
				{
					if (stringValue.Length == 1)
					{
						return stringValue[0];
					}
				}
				else if (targetType == typeof(Uri))
				{
					Uri uri;
					if (Uri.TryCreate(stringValue, UriKind.RelativeOrAbsolute, out uri))
					{
						return uri;
					}
				}
				else if (targetType == typeof(Version))
				{
					// try-catch is pointless since will throw upon generic conversion
					return new Version(stringValue);
				}
			}
			else if (targetType == typeof(TimeSpan))
			{
				return new TimeSpan((long)CoerceType(typeof(long), value));
			}

#if !WINPHONE_8
			TypeConverter converter = TypeDescriptor.GetConverter(targetType);
			if (converter.CanConvertFrom(actualType))
			{
				return converter.ConvertFrom(value);
			}

			converter = TypeDescriptor.GetConverter(actualType);
			if (converter.CanConvertTo(targetType))
			{
				return converter.ConvertTo(value, targetType);
			}
#endif

			try
			{
				// fall back to basics
				return Convert.ChangeType(value, targetType);
			}
			catch (Exception ex)
			{
				throw new JsonTypeCoercionException(
					string.Format("Error converting {0} to {1}",
						new object[] { value.GetType().FullName, targetType.FullName }), ex);
			}
		}

		object CoerceType(TP targetType, IDictionary value, out Dictionary<string, MemberInfo> memberMap)
		{
			object newValue = InstantiateObject(targetType, out memberMap);
			if (memberMap == null)
			{
				return newValue;
			}

			// copy any values into new object
			foreach (object key in value.Keys)
			{
				Type memberType = GetMemberInfo(memberMap, key as string, out MemberInfo memberInfo);
				SetMemberValue(newValue, memberType, memberInfo, value[key]);
			}

			return newValue;
		}

		object CoerceList(TP targetType, TP arrayType, IEnumerable value)
		{
			if (targetType.IsArray)
			{
				return CoerceArray(targetType.GetElementType(), value);
			}

			// targetType serializes as a JSON array but is not an array
			// assume is an ICollection / IEnumerable with AddRange, Add,
			// or custom Constructor with which we can populate it

			// many ICollection types take an IEnumerable or ICollection
			// as a constructor argument.  look through constructors for
			// a compatible match.
			ConstructorInfo[] ctors = targetType.GetConstructors();
			ConstructorInfo defaultCtor = null;
			foreach (ConstructorInfo ctor in ctors)
			{
				ParameterInfo[] paramList = ctor.GetParameters();
				if (paramList.Length == 0)
				{
					// save for in case cannot find closer match
					defaultCtor = ctor;
					continue;
				}

				if (paramList.Length == 1 &&
				    GetTypeInfo(paramList[0].ParameterType).IsAssignableFrom(GetTypeInfo(arrayType)))
				{
					try
					{
						// invoke first constructor that can take this value as an argument
						return ctor.Invoke(new object[] { value });
					}
					catch
					{
						// there might exist a better match
					}
				}
			}

			if (Equals(defaultCtor, null))
			{
				throw new JsonTypeCoercionException(
					string.Format(ErrorDefaultCtor, new object[] { targetType.FullName }));
			}

			object collection;
			try
			{
				// always try-catch Invoke() to expose real exception
				collection = defaultCtor.Invoke(null);
			}
			catch (TargetInvocationException ex)
			{
				if (ex.InnerException != null)
				{
					throw new JsonTypeCoercionException(ex.InnerException.Message, ex.InnerException);
				}

				throw new JsonTypeCoercionException("Error instantiating " + targetType.FullName, ex);
			}

			// many ICollection types have an AddRange method
			// which adds all items at once
#if WINDOWS_STORE
			// \todo Not sure if this finds the correct methods
			MethodInfo method = GetTypeInfo(targetType).GetDeclaredMethod("AddRange");
#else
			MethodInfo method = GetTypeInfo(targetType).GetMethod("AddRange");
#endif

			ParameterInfo[] parameters = method?.GetParameters();
			Type paramType = (parameters == null || parameters.Length != 1) ? null : parameters[0].ParameterType;
			if (!Equals(paramType, null) &&
			    GetTypeInfo(paramType).IsAssignableFrom(GetTypeInfo(arrayType)))
			{
				try
				{
					// always try-catch Invoke() to expose real exception
					// add all members in one method
					method.Invoke(collection,
						new object[] { value });
				}
				catch (TargetInvocationException ex)
				{
					if (ex.InnerException != null)
					{
						throw new JsonTypeCoercionException(ex.InnerException.Message, ex.InnerException);
					}

					throw new JsonTypeCoercionException("Error calling AddRange on " + targetType.FullName, ex);
				}

				return collection;
			}
			// many ICollection types have an Add method
			// which adds items one at a time
#if WINDOWS_STORE
			// \todo Not sure if this finds the correct methods
			method = GetTypeInfo(targetType).GetDeclaredMethod("Add");
#else
			method = GetTypeInfo(targetType).GetMethod("Add");
#endif
			parameters = method?.GetParameters();
			paramType = (parameters == null || parameters.Length != 1) ? null : parameters[0].ParameterType;
			if (!Equals(paramType, null))
			{
				// loop through adding items to collection
				foreach (object item in value)
				{
					try
					{
						// always try-catch Invoke() to expose real exception
						method.Invoke(collection,
							new[]
							{
								CoerceType(paramType, item)
							});
					}
					catch (TargetInvocationException ex)
					{
						if (ex.InnerException != null)
						{
							throw new JsonTypeCoercionException(ex.InnerException.Message, ex.InnerException);
						}

						throw new JsonTypeCoercionException("Error calling Add on " + targetType.FullName, ex);
					}
				}

				return collection;
			}

			try
			{
				// fall back to basics
				return Convert.ChangeType(value, targetType);
			}
			catch (Exception ex)
			{
				throw new JsonTypeCoercionException(
					string.Format("Error converting {0} to {1}",
						new object[] { value.GetType().FullName, targetType.FullName }), ex);
			}
		}

		Array CoerceArray(Type elementType, IEnumerable value)
		{
			//ArrayList target = new ArrayList();

			int count = 0;
			foreach (object item in value)
			{
				count++;
			}

			Array arr = Array.CreateInstance(elementType, new[] { count });

			int i = 0;
			foreach (object item in value)
			{
				//target.Add(CoerceType(elementType, item));
				arr.SetValue(CoerceType(elementType, item), new[] { i });
				i++;
			}

			return arr; //target.ToArray(elementType);
		}

		static bool IsNullable(Type type)
		{
			return GetTypeInfo(type).IsGenericType && (typeof(Nullable<>).Equals(type.GetGenericTypeDefinition()));
		}


		public static bool HasJsonUseTypeHintAttribute(TP tp)
		{
#if WINDOWS_STORE
			return tp.GetCustomAttribute<JsonUseTypeHintAttribute> (true) != null;
#else
			return tp.GetCustomAttributes(typeof(JsonUseTypeHintAttribute), true).Length != 0;
#endif
		}

		#endregion Type Methods
	}
}
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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace DLD.JsonFx
{
	/// <summary>
	/// Reader for consuming JSON data
	/// </summary>
	public class JsonReader
	{
		#region Constants

		internal readonly static string LiteralFalse = "false";
		internal readonly static string LiteralTrue = "true";
		internal readonly static string LiteralNull = "null";
		internal readonly static string LiteralUndefined = "undefined";
		internal readonly static string LiteralNotANumber = "NaN";
		internal readonly static string LiteralPositiveInfinity = "Infinity";
		internal readonly static string LiteralNegativeInfinity = "-Infinity";

		internal const char OperatorNegate = '-';
		internal const char OperatorUnaryPlus = '+';
		internal const char OperatorArrayStart = '[';
		internal const char OperatorArrayEnd = ']';
		internal const char OperatorObjectStart = '{';
		internal const char OperatorObjectEnd = '}';
		internal const char OperatorStringDelim = '"';
		internal const char OperatorStringDelimAlt = '\'';
		internal const char OperatorValueDelim = ',';
		internal const char OperatorNameDelim = ':';
		internal const char OperatorCharEscape = '\\';

		const string CommentStart = "/*";
		const string CommentEnd = "*/";
		const string CommentLine = "//";
		const string LineEndings = "\r\n";

		internal readonly static string TypeGenericIDictionary = "System.Collections.Generic.IDictionary`2";

		const string ErrorUnrecognizedToken = "Illegal JSON sequence.";
		const string ErrorUnterminatedComment = "Unterminated comment block.";
		const string ErrorUnterminatedObject = "Unterminated JSON object.";
		const string ErrorUnterminatedArray = "Unterminated JSON array.";
		const string ErrorUnterminatedString = "Unterminated JSON string.";
		const string ErrorIllegalNumber = "Illegal JSON number.";
		const string ErrorExpectedString = "Expected JSON string.";
		const string ErrorExpectedObject = "Expected JSON object.";
		const string ErrorExpectedArray = "Expected JSON array.";
		const string ErrorExpectedPropertyName = "Expected JSON object property name.";
		const string ErrorExpectedPropertyNameDelim = "Expected JSON object property name delimiter.";
		const string ErrorGenericIDictionary = "Types which implement Generic IDictionary<TKey, TValue> also need to implement IDictionary to be deserialized. ({0})";
		const string ErrorGenericIDictionaryKeys = "Types which implement Generic IDictionary<TKey, TValue> need to have string keys to be deserialized. ({0})";

		#endregion Constants

		#region Fields

		readonly JsonReaderSettings _settings;
		readonly string _source;
		readonly int _sourceLength;
		int _index;

		int _depth;

		/// <summary>
		/// List of previously deserialized objects.
		/// Used for reference cycle handling.
		/// </summary>
		readonly List<object> _previouslyDeserialized = new List<object>();

		/// <summary>
		/// Cache ArrayLists.
		/// Otherwise every new deserialization of an array will allocate a new ArrayList.
		/// </summary>
		readonly Stack<List<object>> _jsArrays = new Stack<List<object>>();

		public ReferenceHandlerReader ReferenceHandler;

		/// <summary>
		/// True if there is nothing more to deserialize
		/// </summary>
		public bool Eof => _index >= _sourceLength - 1;

		#endregion Fields

		#region Init

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="input">TextReader containing source</param>
		public JsonReader(TextReader input)
			: this(input, new JsonReaderSettings())
		{
		}

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="input">TextReader containing source</param>
		/// <param name="settings">JsonReaderSettings</param>
		public JsonReader(TextReader input, JsonReaderSettings settings)
		{
			_settings = settings;
			_source = input.ReadToEnd();
			_sourceLength = _source.Length;
		}

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="input">Stream containing source</param>
		public JsonReader(Stream input)
			: this(input, new JsonReaderSettings())
		{
		}

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="input">Stream containing source</param>
		/// <param name="settings">JsonReaderSettings</param>
		public JsonReader(Stream input, JsonReaderSettings settings)
		{
			_settings = settings;

			using (StreamReader reader = new StreamReader(input, true))
			{
				_source = reader.ReadToEnd();
			}

			_sourceLength = _source.Length;
		}

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="input">string containing source</param>
		public JsonReader(string input)
			: this(input, new JsonReaderSettings())
		{
		}

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="input">string containing source</param>
		/// <param name="settings">JsonReaderSettings</param>
		public JsonReader(string input, JsonReaderSettings settings)
		{
			_settings = settings;
			_source = input;
			_sourceLength = _source.Length;
		}

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="input">StringBuilder containing source</param>
		public JsonReader(StringBuilder input)
			: this(input, new JsonReaderSettings())
		{
		}

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="input">StringBuilder containing source</param>
		/// <param name="settings">JsonReaderSettings</param>
		public JsonReader(StringBuilder input, JsonReaderSettings settings)
		{
			_settings = settings;
			_source = input.ToString();
			_sourceLength = _source.Length;
		}

		#endregion Init

		#region Properties

		#endregion Properties

		#region Parsing Methods

		/// <summary>
		/// Convert from JSON string to Object graph
		/// </summary>
		/// <returns></returns>
		public object Deserialize()
		{
			return Deserialize((TP)null);
		}

		/// <summary>
		/// Convert from JSON string to Object graph
		/// </summary>
		/// <returns></returns>
		public object Deserialize(int start)
		{
			_index = start;

			return Deserialize((TP)null);
		}

		/// <summary>
		/// Convert from JSON string to Object graph of specific Type
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public object Deserialize(TP type)
		{
			_depth = -1;

			// should this run through a preliminary test here?
			return Read(type, false);
		}

		/// <summary>
		/// Convert from JSON string to Object graph of specific Type
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public object Deserialize(int start, TP type)
		{
			_index = start;

			_depth = -1;

			// should this run through a preliminary test here?
			return Read(type, false);
		}

		public object Read(TP expectedType, bool typeIsHint)
		{
			_depth++;

			if (expectedType == typeof(object))
			{
				expectedType = null;
			}

			JsonToken token = Tokenize();

			if (!Equals(expectedType, null) && !expectedType.IsPrimitive)
			{
				JsonConverter converter = _settings.GetConverter(expectedType);
				if (converter != null && (_depth > 0 || converter.ConvertAtDepthZero))
				{
					object val;
					try
					{
						val = Read(typeof(Dictionary<string, object>), false);
						Dictionary<string, object> dict = val as Dictionary<string, object>;
						if (dict == null) return null;
						object obj = converter.Read(this, expectedType, dict);

						_depth--;
						return obj;
					}
					catch (JsonTypeCoercionException e)
					{
#if DEBUG
						Console.WriteLine("Could not cast to dictionary for converter processing. Ignoring field.\n" + e);
#endif
					}

					_depth--;
					return null;
				}

				if (typeof(IJsonSerializable).IsAssignableFrom(expectedType))
				{
					IJsonSerializable res = _settings.Coercion.InstantiateObject(expectedType) as IJsonSerializable;
					res.ReadJson(this);

					_depth--;
					return res;
				}
			}

			object result = null;
			switch (token)
			{
				case JsonToken.ObjectStart:
				{
					result = ReadObject(typeIsHint ? null : expectedType);

					_depth--;
					return result;
				}
				case JsonToken.ArrayStart:
				{
					result = ReadArray(typeIsHint ? null : expectedType);

					_depth--;
					return result;
				}
				case JsonToken.String:
				{
					result = ReadString(typeIsHint ? null : expectedType);

					_depth--;
					return result;
				}
				case JsonToken.Number:
				{
					result = ReadNumber(typeIsHint ? null : expectedType);

					_depth--;
					return result;
				}
				case JsonToken.False:
				{
					_index += LiteralFalse.Length;

					_depth--;
					return false;
				}
				case JsonToken.True:
				{
					_index += LiteralTrue.Length;

					_depth--;
					return true;
				}
				case JsonToken.Null:
				{
					_index += LiteralNull.Length;

					_depth--;
					return null;
				}
				case JsonToken.NaN:
				{
					_index += LiteralNotANumber.Length;

					_depth--;
					return double.NaN;
				}
				case JsonToken.PositiveInfinity:
				{
					_index += LiteralPositiveInfinity.Length;

					_depth--;
					return double.PositiveInfinity;
				}
				case JsonToken.NegativeInfinity:
				{
					_index += LiteralNegativeInfinity.Length;

					_depth--;
					return double.NegativeInfinity;
				}
				case JsonToken.Undefined:
				{
					_index += LiteralUndefined.Length;

					_depth--;
					return null;
				}
				case JsonToken.End:
				default:
				{
					_depth--;
					return null;
				}
			}
		}

		/// <summary>
		/// Populates an object with serialized data.
		/// </summary>
		/// <remarks>
		/// Note that in case the object has been loaded before (another reference to it)
		/// the passed object will be changed to the previously loaded object (this only applies
		/// if you have enabled CyclicReferenceHandling in the settings).
		/// </remarks>
		/// <param name="obj"></param>
		/// <typeparam name="T"></typeparam>
		public void PopulateObject<T>(ref T obj) where T : class
		{
			object ob = obj;
			_depth = 0;

			// Eat whitespace and comments
			Tokenize();

			PopulateObject(ref ob);
			obj = ob as T;
		}

		/// <summary>
		/// Populates an object with serialized data.
		/// </summary>
		/// <remarks>
		/// Note that in case the object has been loaded before (another reference to it)
		/// the passed object will be changed to the previously loaded object (this only applies
		/// if you have enabled CyclicReferenceHandling in the settings).
		/// </remarks>
		/// <param name="obj"></param>
		void PopulateObject(ref object obj)
		{
			Type objectType = obj.GetType();
			Dictionary<string, MemberInfo> memberMap = _settings.Coercion.GetMemberMap(objectType);
			Type genericDictionaryType = null;

			if (memberMap == null)
			{
				genericDictionaryType = GetGenericDictionaryType(objectType);
			}

			_depth = 0;
			PopulateObject(ref obj, objectType, memberMap, genericDictionaryType);
		}

		object ReadObject(TP objectType)
		{
			Type genericDictionaryType = null;
			Dictionary<string, MemberInfo> memberMap = null;
			object result;

			if (!Equals(objectType, null))
			{
				if (objectType.IsAbstract)
				{
					// The type is abstract
					// This means we cannot directly instantiate it
					// So leave it as null and hope that the
					// PopulateObject method finds a type hint
					result = null;
				}
				else
				{
					result = _settings.Coercion.InstantiateObject(objectType, out memberMap);
				}

				if (_settings.HandleCyclicReferences)
				{
					_previouslyDeserialized.Add(result);
				}

				if (memberMap == null)
				{
					genericDictionaryType = GetGenericDictionaryType(objectType);
				}
			}
			else
			{
				result = new Dictionary<string, object>();
			}

			object prev = result;
			PopulateObject(ref result, objectType, memberMap, genericDictionaryType);

			if (_settings.HandleCyclicReferences && prev != result && !Equals(objectType, null))
			{
				// If prev != result, then the PopulateObject method has used a previously loaded object
				// then we should not add the object to the list of deserialized objects since it
				// already is there (the correct version of it, that is)
				// TODO: Is this correct? Will the PopulateObject method not add more stuff  the the list
				_previouslyDeserialized.RemoveAt(_previouslyDeserialized.Count - 1);
			}

			return result;
		}

		TP GetGenericDictionaryType(TP objectType)
		{
			// this allows specific IDictionary<string, T> to deserialize T
#if !WINPHONE_8
			Type genericDictionary = TypeCoercionUtility.GetTypeInfo(objectType).GetInterface(TypeGenericIDictionary);
			if (genericDictionary != null)
			{
				Type[] genericArgs = genericDictionary.GetGenericArguments();
				if (genericArgs.Length == 2)
				{
					if (!(genericArgs[0] == typeof(string)))
					{
						throw new JsonDeserializationException(
							string.Format(ErrorGenericIDictionaryKeys, new object[] { objectType }),
							_index);
					}

					if (!(genericArgs[1] == typeof(object)))
					{
						return genericArgs[1];
					}
				}
			}
#endif
			return null;
		}

		void PopulateObject(ref object result, TP objectType, Dictionary<string, MemberInfo> memberMap,
			TP genericDictionaryType)
		{
			if (_source[_index] != OperatorObjectStart)
			{
				throw new JsonDeserializationException(ErrorExpectedObject, _index);
			}

#if WINPHONE_8
			IDictionary idict = result as IDictionary;
#else
			IDictionary idict = result as IDictionary;

			if (idict == null &&
			    !Equals(TypeCoercionUtility.GetTypeInfo(objectType).GetInterface(TypeGenericIDictionary), null))
			{
				throw new JsonDeserializationException(
					string.Format(ErrorGenericIDictionary, new object[] { objectType }),
					_index);
			}
#endif

			JsonToken token;
			do
			{
				Type memberType;
				MemberInfo memberInfo;

				// consume opening brace or delim
				_index++;
				if (_index >= _sourceLength)
				{
					throw new JsonDeserializationException(ErrorUnterminatedObject, _index);
				}

				// get next token
				token = Tokenize(_settings.AllowUnquotedObjectKeys);
				if (token == JsonToken.ObjectEnd)
				{
					break;
				}

				if (token != JsonToken.String && token != JsonToken.UnquotedName)
				{
					throw new JsonDeserializationException(ErrorExpectedPropertyName, _index);
				}

				// parse object member value
				string memberName = (token == JsonToken.String) ? (string)ReadString(null) : ReadUnquotedKey();

				//
				if (Equals(genericDictionaryType, null) && memberMap != null)
				{
					// determine the type of the property/field
					memberType = TypeCoercionUtility.GetMemberInfo(memberMap, memberName, out memberInfo);
				}
				else
				{
					memberType = genericDictionaryType;
					memberInfo = null;
				}

				// get next token
				token = Tokenize();
				if (token != JsonToken.NameDelim)
				{
					throw new JsonDeserializationException(ErrorExpectedPropertyNameDelim, _index);
				}

				// consume delim
				_index++;
				if (_index >= _sourceLength)
				{
					throw new JsonDeserializationException(ErrorUnterminatedObject, _index);
				}

				object value;

				// Reference to previously deserialized value
				if (_settings.HandleCyclicReferences && memberName == "@ref")
				{
					// parse object member value
					int refId = (int)Read(typeof(int), false);

					// Change result object to the one previously deserialized
					result = _previouslyDeserialized[refId];
					// get next token
					// this will probably be the end of the object
					token = Tokenize();
					continue;
				}

				if (memberName == "@tag")
				{
					// parse object member value
					int idx = (int)Read(typeof(int), false);

					if (ReferenceHandler == null)
					{
						throw new Exception("Encountered a @tag in the data but no reference handler has been provided");
					}

					ReferenceHandler.Set(idx, result);

					// get next token
					token = Tokenize();
					continue;
				}
				// Normal serialized value

				// parse object member value
				value = Read(memberType, false);

				if (value != null &&
				    value.GetType() == typeof(string) &&
				    !Equals(memberType, null) &&
				    !(memberType == typeof(string)))
				{
					// We got a string, but we did not expect it
					// Is it a reference?
					var str = value as string;
					if (str.StartsWith("@"))
					{
						int idx;
						if (int.TryParse(str.Substring(1), out idx))
						{
							// Found reference
							if (!ReferenceHandler.TryGetValueFromID(idx, out value))
							{
								// Reference has not been deserialized yet, add a delayed callback

								if (idict != null)
								{
									ReferenceHandler.AddDelayedDictionarySetter(idx, idict, memberName);
								}
								else
								{
									ReferenceHandler.AddDelayedSetter(idx, memberInfo, result);
								}

								value = null;
							}
						}
						else
						{
							throw new JsonDeserializationException(
								"Expected " +
								memberType.Name +
								" but got a string. It looked like a reference, but the id could not be parsed: '" +
								str +
								"'", _index);
						}
					}
					else
					{
						throw new JsonDeserializationException(
							"Expected " + memberType.Name + " but got a string. This stage should not have been reached.",
							_index);
					}
				}

				// We reached this point without having seen a type hint
				// And our object we were trying to populate was null
				// That's bad. Type hints are always first in the data
				if (result == null && !_settings.IsTypeHintName(memberName))
				{
					throw new JsonDeserializationException("Cannot populate null object of type " +
					                                       (objectType != null ? objectType.Name : "<null>") +
					                                       " with member name " +
					                                       memberName +
					                                       ".\n" +
					                                       "Likely we were trying to deserialize an abstract class which cannot be instantiated and no type hint was found in the data",
						_index);
				}

				if (idict != null)
				{
					if (Equals(objectType, null) && _settings.IsTypeHintName(memberName))
					{
						result = _settings.Coercion.ProcessTypeHint(idict, value as string,
							_settings.AssemblyNamesToSearchThroughIfNotFound, _settings.SearchThroughAllAssembliesIfNotFound,
							ref objectType, ref memberMap);
					}
					else
					{
						idict[memberName] = value;
					}
				}
				else
				{
					if (_settings.IsTypeHintName(memberName))
					{
						result = _settings.Coercion.ProcessTypeHint(result, value as string,
							_settings.AssemblyNamesToSearchThroughIfNotFound, _settings.SearchThroughAllAssembliesIfNotFound,
							ref objectType, ref memberMap);
					}
					else
					{
						_settings.Coercion.SetMemberValue(result, memberType, memberInfo, value);
					}
				}

				// get next token
				token = Tokenize();
			} while (token == JsonToken.ValueDelim);

			if (token != JsonToken.ObjectEnd)
			{
				throw new JsonDeserializationException(ErrorUnterminatedObject, _index);
			}

			// consume closing brace
			_index++;

			//return result;
		}

		IEnumerable ReadArray(TP arrayType)
		{
			if (_source[_index] != OperatorArrayStart)
			{
				throw new JsonDeserializationException(ErrorExpectedArray, _index);
			}


			bool isArrayItemTypeSet = (!Equals(arrayType, null));
			bool isArrayTypeAHint = !isArrayItemTypeSet;
			Type arrayItemType = null;

			if (isArrayItemTypeSet)
			{
				if (arrayType.HasElementType)
				{
					arrayItemType = arrayType.GetElementType();
				}
				else if (TypeCoercionUtility.GetTypeInfo(arrayType).IsGenericType)
				{
					Type[] generics = arrayType.GetGenericArguments();
					if (generics.Length == 1)
					{
						// could use the first or last, but this more correct
						arrayItemType = generics[0];
					}
				}
			}

			// Get a temporary buffer from a cache
			List<object> buffer = _jsArrays.Count > 0 ? _jsArrays.Pop() : new List<object>();
			buffer.Clear();

			List<KeyValuePair<int, int>> delayedReferences = null;

			JsonToken token;
			do
			{
				// consume opening bracket or delim
				_index++;
				if (_index >= _sourceLength)
				{
					throw new JsonDeserializationException(ErrorUnterminatedArray, _index);
				}

				// get next token
				token = Tokenize();
				if (token == JsonToken.ArrayEnd)
				{
					break;
				}

				// parse array item
				object value = Read(arrayItemType, isArrayTypeAHint);

				// Check for references
				if (value != null &&
				    ReferenceHandler != null &&
				    value.GetType() == typeof(string) &&
				    !Equals(arrayItemType, null) &&
				    !(arrayItemType == typeof(string)))
				{
					if ((value as string).StartsWith("@"))
					{
						int idx;
						if (int.TryParse((value as string).Substring(1), out idx))
						{
							// Found reference
							if (!ReferenceHandler.TryGetValueFromID(idx, out value))
							{
								// Reference has not been deserialized yet, add a delayed callback

								if (delayedReferences == null) delayedReferences = new List<KeyValuePair<int, int>>();
								delayedReferences.Add(new KeyValuePair<int, int>(buffer.Count, idx));

								// Add null to the array in the meantime
								value = null;
							}
						}
						else
						{
							throw new Exception("Expected " +
							                    arrayItemType.Name +
							                    " but got a string. It looked like a reference, but the id could not be parsed: '" +
							                    value +
							                    "'");
						}
					}
					else
					{
						throw new Exception("Should not be reached");
					}
				}

				buffer.Add(value);

				// establish if array is of common type
				if (value == null)
				{
					if (!Equals(arrayItemType, null) && TypeCoercionUtility.GetTypeInfo(arrayItemType).IsValueType)
					{
						// use plain object to hold null
						arrayItemType = null;
					}

					isArrayItemTypeSet = true;
				}
				else if (!Equals(arrayItemType, null) &&
				         !TypeCoercionUtility.GetTypeInfo(arrayItemType)
					         .IsAssignableFrom(TypeCoercionUtility.GetTypeInfo(value.GetType())))
				{
					if (TypeCoercionUtility.GetTypeInfo(value.GetType())
					    .IsAssignableFrom(TypeCoercionUtility.GetTypeInfo(arrayItemType)))
					{
						// attempt to use the more general type
						arrayItemType = value.GetType();
					}
					else
					{
						// use plain object to hold value
						arrayItemType = null;
						isArrayItemTypeSet = true;
					}
				}
				else if (!isArrayItemTypeSet)
				{
					// try out a hint type
					// if hasn't been set before
					arrayItemType = value.GetType();
					isArrayItemTypeSet = true;
				}

				// get next token
				token = Tokenize();
			} while (token == JsonToken.ValueDelim);

			if (token != JsonToken.ArrayEnd)
			{
				throw new JsonDeserializationException(ErrorUnterminatedArray, _index);
			}

			// consume closing bracket
			_index++;

			// TODO: optimize to reduce number of conversions on lists

			_jsArrays.Push(buffer);

			IList result;

			if (!Equals(arrayItemType, null) && !(arrayItemType == typeof(object)))
			{
				if (arrayType != null && arrayType.IsGenericType && arrayType.GetGenericTypeDefinition() == typeof(List<>))
				{
					// A generic list
					IList list = Activator.CreateInstance(arrayType, buffer.Count) as IList;
					for (int i = 0; i < buffer.Count; i++)
					{
						list.Add(buffer[i]);
					}

					result = list;
				}
				else
				{
					// A typed array (not System.Object)

					// if all items are of same type then convert to array of that type
					Array arr = Array.CreateInstance(arrayItemType, new[] { buffer.Count });
					for (int i = 0; i < buffer.Count; i++)
						arr.SetValue(buffer[i], new[] { i });

					result = arr;
				}
			}
			else
			{
				// convert to an object array for consistency
				result = buffer.ToArray();
			}

			if (delayedReferences != null)
			{
				for (int i = 0; i < delayedReferences.Count; i++)
				{
					ReferenceHandler.AddDelayedListSetter(delayedReferences[i].Value, result, delayedReferences[i].Key);
				}
			}

			return result;
		}

		/// <summary>
		/// Reads an unquoted JSON object key
		/// </summary>
		/// <returns></returns>
		string ReadUnquotedKey()
		{
			int start = _index;
			do
			{
				// continue scanning until reach a valid token
				_index++;
			} while (Tokenize(true) == JsonToken.UnquotedName);

			return _source.Substring(start, _index - start);
		}

		readonly StringBuilder _builder = new StringBuilder();

		/// <summary>
		/// Reads a JSON string
		/// </summary>
		/// <param name="expectedType"></param>
		/// <returns>string or value which is represented as a string in JSON</returns>
		object ReadString(TP expectedType)
		{
			if (_source[_index] != OperatorStringDelim &&
			    _source[_index] != OperatorStringDelimAlt)
			{
				throw new JsonDeserializationException(ErrorExpectedString, _index);
			}

			char startStringDelim = _source[_index];

			// consume opening quote
			_index++;
			if (_index >= _sourceLength)
			{
				throw new JsonDeserializationException(ErrorUnterminatedString, _index);
			}

			_builder.Length = 0;
			int start = _index;

			while (_source[_index] != startStringDelim)
			{
				if (_source[_index] == OperatorCharEscape)
				{
					// copy chunk before decoding
					_builder.Append(_source, start, _index - start);

					// consume escape char
					_index++;
					if (_index >= _sourceLength)
					{
						throw new JsonDeserializationException(ErrorUnterminatedString, _index);
					}

					// decode
					switch (_source[_index])
					{
						case '0':
						{
							// don't allow NULL char '\0'
							// causes CStrings to terminate
							break;
						}
						case 'b':
						{
							// backspace
							_builder.Append('\b');
							break;
						}
						case 'f':
						{
							// formfeed
							_builder.Append('\f');
							break;
						}
						case 'n':
						{
							// newline
							_builder.Append('\n');
							break;
						}
						case 'r':
						{
							// carriage return
							_builder.Append('\r');
							break;
						}
						case 't':
						{
							// tab
							_builder.Append('\t');
							break;
						}
						case 'u':
						{
							// Unicode escape sequence
							// e.g. Copyright: "\u00A9"

							// unicode ordinal
							int utf16;
							if (_index + 4 < _sourceLength &&
							    int.TryParse(_source.Substring(_index + 1, 4),
								    NumberStyles.AllowHexSpecifier,
								    NumberFormatInfo.InvariantInfo,
								    out utf16))
							{
								_builder.Append(char.ConvertFromUtf32(utf16));
								_index += 4;
							}
							else
							{
								// using FireFox style recovery, if not a valid hex
								// escape sequence then treat as single escaped 'u'
								// followed by rest of string
								_builder.Append(_source[_index]);
							}

							break;
						}
						default:
						{
							_builder.Append(_source[_index]);
							break;
						}
					}

					_index++;
					if (_index >= _sourceLength)
					{
						throw new JsonDeserializationException(ErrorUnterminatedString, _index);
					}

					start = _index;
				}
				else
				{
					// next char
					_index++;
					if (_index >= _sourceLength)
					{
						throw new JsonDeserializationException(ErrorUnterminatedString, _index);
					}
				}
			}

			// copy rest of string
			_builder.Append(_source, start, _index - start);

			// consume closing quote
			_index++;

			string output = _builder.ToString();

			if (!Equals(expectedType, null) && !(expectedType == typeof(string)))
			{
				// We did not expect this type
				// Is it possibly a reference (formatted as @int)
				if (output.StartsWith("@"))
				{
					// Ok
					return output;
				}

				// Try to convert the type
				return _settings.Coercion.CoerceType(expectedType, output);
			}

			return output;
		}

		object ReadNumber(TP expectedType)
		{
			bool hasDecimal = false;
			bool hasExponent = false;
			int start = _index;
			int precision = 0;
			int exponent = 0;

			// optional minus part
			if (_source[_index] == OperatorNegate)
			{
				// consume sign
				_index++;
				if (_index >= _sourceLength || !char.IsDigit(_source[_index]))
					throw new JsonDeserializationException(ErrorIllegalNumber, _index);
			}

			// integer part
			while ((_index < _sourceLength) && char.IsDigit(_source[_index]))
			{
				// consume digit
				_index++;
			}

			// optional decimal part
			if ((_index < _sourceLength) && (_source[_index] == '.'))
			{
				hasDecimal = true;

				// consume decimal
				_index++;
				if (_index >= _sourceLength || !char.IsDigit(_source[_index]))
				{
					throw new JsonDeserializationException(ErrorIllegalNumber, _index);
				}

				// fraction part
				while (_index < _sourceLength && char.IsDigit(_source[_index]))
				{
					// consume digit
					_index++;
				}
			}

			// note the number of significant digits
			precision = _index - start - (hasDecimal ? 1 : 0);

			// optional exponent part
			if (_index < _sourceLength && (_source[_index] == 'e' || _source[_index] == 'E'))
			{
				hasExponent = true;

				// consume 'e'
				_index++;
				if (_index >= _sourceLength)
				{
					throw new JsonDeserializationException(ErrorIllegalNumber, _index);
				}

				int expStart = _index;

				// optional minus/plus part
				if (_source[_index] == OperatorNegate || _source[_index] == OperatorUnaryPlus)
				{
					// consume sign
					_index++;
					if (_index >= _sourceLength || !char.IsDigit(_source[_index]))
					{
						throw new JsonDeserializationException(ErrorIllegalNumber, _index);
					}
				}
				else
				{
					if (!char.IsDigit(_source[_index]))
					{
						throw new JsonDeserializationException(ErrorIllegalNumber, _index);
					}
				}

				// exp part
				while (_index < _sourceLength && char.IsDigit(_source[_index]))
				{
					// consume digit
					_index++;
				}

				int.TryParse(_source.Substring(expStart, _index - expStart), NumberStyles.Integer,
					NumberFormatInfo.InvariantInfo, out exponent);
			}

			// at this point, we have the full number string and know its characteristics
			string numberString = _source.Substring(start, _index - start);

			if (!hasDecimal && !hasExponent && precision < 19)
			{
				// is Integer value

				// parse as most flexible
				decimal number = decimal.Parse(numberString,
					NumberStyles.Integer,
					NumberFormatInfo.InvariantInfo);


				if (!Equals(expectedType, null))
				{
					return _settings.Coercion.CoerceType(expectedType, number);
				}

				if (number >= int.MinValue && number <= int.MaxValue)
				{
					// use most common
					return (int)number;
				}

				if (number >= long.MinValue && number <= long.MaxValue)
				{
					// use more flexible
					return (long)number;
				}

				// use most flexible
				return number;
			}
			else
			{
				// is Floating Point value

				if (expectedType == typeof(decimal))
				{
					// special case since Double does not convert to Decimal
					return decimal.Parse(numberString,
						NumberStyles.Float,
						NumberFormatInfo.InvariantInfo);
				}

				// use native EcmaScript number (IEEE 754)
				double number = double.Parse(numberString,
					NumberStyles.Float,
					NumberFormatInfo.InvariantInfo);

				if (!Equals(expectedType, null))
				{
					return _settings.Coercion.CoerceType(expectedType, number);
				}

				return number;
			}
		}

		#endregion Parsing Methods

		#region Static Methods

		/// <summary>
		/// A fast method for deserializing an object from JSON
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static object Deserialize(string value)
		{
			return Deserialize(value, 0, null);
		}

		/// <summary>
		/// A fast method for deserializing an object from JSON
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="value"></param>
		/// <returns></returns>
		public static T Deserialize<T>(string value)
		{
			return (T)Deserialize(value, 0, typeof(T));
		}

		/// <summary>
		/// A fast method for deserializing an object from JSON
		/// </summary>
		/// <param name="value"></param>
		/// <param name="start"></param>
		/// <returns></returns>
		public static object Deserialize(string value, int start)
		{
			return Deserialize(value, start, null);
		}

		/// <summary>
		/// A fast method for deserializing an object from JSON
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="value"></param>
		/// <param name="start"></param>
		/// <returns></returns>
		public static T Deserialize<T>(string value, int start)
		{
			return (T)Deserialize(value, start, typeof(T));
		}

		/// <summary>
		/// A fast method for deserializing an object from JSON
		/// </summary>
		/// <param name="value"></param>
		/// <param name="type"></param>
		/// <returns></returns>
		public static object Deserialize(string value, TP type)
		{
			return Deserialize(value, 0, type);
		}

		/// <summary>
		/// A fast method for deserializing an object from JSON
		/// </summary>
		/// <param name="value">source text</param>
		/// <param name="start">starting position</param>
		/// <param name="type">expected type</param>
		/// <returns></returns>
		public static object Deserialize(string value, int start, TP type)
		{
			return (new JsonReader(value)).Deserialize(start, type);
		}

		#endregion Static Methods

		#region Tokenizing Methods

		JsonToken Tokenize()
		{
			// unquoted object keys are only allowed in object properties
			return Tokenize(false);
		}

		JsonToken Tokenize(bool allowUnquotedString)
		{
			if (_index >= _sourceLength)
			{
				return JsonToken.End;
			}

			// skip whitespace
			while (char.IsWhiteSpace(_source[_index]))
			{
				_index++;
				if (_index >= _sourceLength)
				{
					return JsonToken.End;
				}
			}

			#region Skip Comments

			// skip block and line comments
			if (_source[_index] == CommentStart[0])
			{
				if (_index + 1 >= _sourceLength)
				{
					throw new JsonDeserializationException(
						ErrorUnrecognizedToken + " (end of stream while parsing possible comment)", _index);
				}

				// skip over first char of comment start
				_index++;

				bool isBlockComment = false;
				if (_source[_index] == CommentStart[1])
				{
					isBlockComment = true;
				}
				else if (_source[_index] != CommentLine[1])
				{
					throw new JsonDeserializationException(ErrorUnrecognizedToken, _index);
				}

				// skip over second char of comment start
				_index++;

				if (isBlockComment)
				{
					// store index for unterminated case
					int commentStart = _index - 2;

					if (_index + 1 >= _sourceLength)
					{
						throw new JsonDeserializationException(ErrorUnterminatedComment, commentStart);
					}

					// skip over everything until reach block comment ending
					while (_source[_index] != CommentEnd[0] ||
					       _source[_index + 1] != CommentEnd[1])
					{
						_index++;
						if (_index + 1 >= _sourceLength)
						{
							throw new JsonDeserializationException(ErrorUnterminatedComment, commentStart);
						}
					}

					// skip block comment end token
					_index += 2;
					if (_index >= _sourceLength)
					{
						return JsonToken.End;
					}
				}
				else
				{
					// skip over everything until reach line ending
					while (LineEndings.IndexOf(_source[_index]) < 0)
					{
						_index++;
						if (_index >= _sourceLength)
						{
							return JsonToken.End;
						}
					}
				}

				// skip whitespace again
				while (char.IsWhiteSpace(_source[_index]))
				{
					_index++;
					if (_index >= _sourceLength)
					{
						return JsonToken.End;
					}
				}
			}

			#endregion Skip Comments

			// consume positive signing (as is extraneous)
			if (_source[_index] == OperatorUnaryPlus)
			{
				_index++;
				if (_index >= _sourceLength)
				{
					return JsonToken.End;
				}
			}

			switch (_source[_index])
			{
				case OperatorArrayStart:
				{
					return JsonToken.ArrayStart;
				}
				case OperatorArrayEnd:
				{
					return JsonToken.ArrayEnd;
				}
				case OperatorObjectStart:
				{
					return JsonToken.ObjectStart;
				}
				case OperatorObjectEnd:
				{
					return JsonToken.ObjectEnd;
				}
				case OperatorStringDelim:
				case OperatorStringDelimAlt:
				{
					return JsonToken.String;
				}
				case OperatorValueDelim:
				{
					return JsonToken.ValueDelim;
				}
				case OperatorNameDelim:
				{
					return JsonToken.NameDelim;
				}
			}

			// number
			if (char.IsDigit(_source[_index]) ||
			    ((_source[_index] == OperatorNegate) && (_index + 1 < _sourceLength) && char.IsDigit(_source[_index + 1])))
			{
				return JsonToken.Number;
			}

			// "false" literal
			if (MatchLiteral(LiteralFalse))
			{
				return JsonToken.False;
			}

			// "true" literal
			if (MatchLiteral(LiteralTrue))
			{
				return JsonToken.True;
			}

			// "null" literal
			if (MatchLiteral(LiteralNull))
			{
				return JsonToken.Null;
			}

			// "NaN" literal
			if (MatchLiteral(LiteralNotANumber))
			{
				return JsonToken.NaN;
			}

			// "Infinity" literal
			if (MatchLiteral(LiteralPositiveInfinity))
			{
				return JsonToken.PositiveInfinity;
			}

			// "-Infinity" literal
			if (MatchLiteral(LiteralNegativeInfinity))
			{
				return JsonToken.NegativeInfinity;
			}

			// "undefined" literal
			if (MatchLiteral(LiteralUndefined))
			{
				return JsonToken.Undefined;
			}

			if (allowUnquotedString)
			{
				return JsonToken.UnquotedName;
			}


			string around = _source.Substring(Math.Max(0, _index - 5), Math.Min(_sourceLength - _index - 1, 20));
			throw new JsonDeserializationException(
				ErrorUnrecognizedToken +
				" (when parsing '" +
				_source[_index] +
				"' " +
				(int)_source[_index] +
				") at index " +
				_index +
				"\nAround: '" +
				around +
				"'", _index);
		}

		/// <summary>
		/// Determines if the next token is the given literal
		/// </summary>
		/// <param name="literal"></param>
		/// <returns></returns>
		bool MatchLiteral(string literal)
		{
			int literalLength = literal.Length;
			if (_index + literalLength > _sourceLength) return false;

			for (int i = 0; i < literalLength; i++)
			{
				if (literal[i] != _source[_index + i])
				{
					return false;
				}
			}

			return true;
		}

		#endregion Tokenizing Methods

		#region Type Methods

		/// <summary>
		/// Converts a value into the specified type using type inference.
		/// </summary>
		/// <typeparam name="T">target type</typeparam>
		/// <param name="value">value to convert</param>
		/// <param name="typeToMatch">example object to get the type from</param>
		/// <returns></returns>
		public static T CoerceType<T>(object value, T typeToMatch)
		{
			return (T)new TypeCoercionUtility().CoerceType(typeof(T), value);
		}

		/// <summary>
		/// Converts a value into the specified type.
		/// </summary>
		/// <typeparam name="T">target type</typeparam>
		/// <param name="value">value to convert</param>
		/// <returns></returns>
		public static T CoerceType<T>(object value)
		{
			return (T)new TypeCoercionUtility().CoerceType(typeof(T), value);
		}

		/// <summary>
		/// Converts a value into the specified type.
		/// </summary>
		/// <param name="targetType">target type</param>
		/// <param name="value">value to convert</param>
		/// <returns></returns>
		public static object CoerceType(TP targetType, object value)
		{
			return new TypeCoercionUtility().CoerceType(targetType, value);
		}

		#endregion Type Methods
	}
}
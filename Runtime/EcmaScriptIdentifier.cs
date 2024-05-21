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

#if !NO_ECMASCRIPT
namespace DLD.JsonFx
{
	/// <summary>
	/// Represents an ECMAScript identifier for serialization.
	/// </summary>
	public class EcmaScriptIdentifier : IJsonSerializable
	{
		#region Fields

		readonly string _identifier;

		#endregion Fields

		#region Init

		/// <summary>
		/// Ctor
		/// </summary>
		public EcmaScriptIdentifier()
			: this(null)
		{
		}

		/// <summary>
		/// Ctor
		/// </summary>
		public EcmaScriptIdentifier(string ident)
		{
			_identifier = string.IsNullOrEmpty(ident) ? string.Empty : EnsureValidIdentifier(ident, true);
		}

		#endregion Init

		#region Properties

		/// <summary>
		/// Gets the ECMAScript identifier represented by this instance.
		/// </summary>
		public string Identifier => _identifier;

		#endregion Properties

		#region Methods

		/// <summary>
		/// Ensures is a valid EcmaScript variable expression.
		/// </summary>
		/// <param name="varExpr">the variable expression</param>
		/// <returns>varExpr</returns>
		public static string EnsureValidIdentifier(string varExpr, bool nested)
		{
			return EnsureValidIdentifier(varExpr, nested, true);
		}

		/// <summary>
		/// Ensures is a valid EcmaScript variable expression.
		/// </summary>
		/// <param name="varExpr">the variable expression</param>
		/// <returns>varExpr</returns>
		public static string EnsureValidIdentifier(string varExpr, bool nested, bool throwOnEmpty)
		{
			if (string.IsNullOrEmpty(varExpr))
			{
				if (throwOnEmpty)
				{
					throw new ArgumentException("Variable expression is empty.");
				}

				return string.Empty;
			}

			varExpr = varExpr.Replace(" ", "");

			if (!IsValidIdentifier(varExpr, nested))
			{
				throw new ArgumentException("Variable expression \"" + varExpr + "\" is not supported.");
			}

			return varExpr;
		}

		/// <summary>
		/// Verifies is a valid EcmaScript variable expression.
		/// </summary>
		/// <param name="varExpr">the variable expression</param>
		/// <returns>varExpr</returns>
		/// <remarks>
		/// http://www.ecma-international.org/publications/files/ECMA-ST/Ecma-262.pdf
		///
		/// IdentifierName =
		///		IdentifierStart | IdentifierName IdentifierPart
		/// IdentifierStart =
		///		Letter | '$' | '_'
		/// IdentifierPart =
		///		IdentifierStart | Digit
		/// </remarks>
		public static bool IsValidIdentifier(string varExpr, bool nested)
		{
			if (string.IsNullOrEmpty(varExpr))
			{
				return false;
			}

			if (nested)
			{
				string[] parts = varExpr.Split('.');
				foreach (string part in parts)
				{
					if (!IsValidIdentifier(part, false))
					{
						return false;
					}
				}

				return true;
			}

			if (IsReservedWord(varExpr))
			{
				return false;
			}

			bool indentPart = false;
			foreach (char ch in varExpr)
			{
				if (indentPart && char.IsDigit(ch))
				{
					// digits are only allowed after first char
					continue;
				}

				// can be start or part
				if (char.IsLetter(ch) || ch == '_' || ch == '$')
				{
					indentPart = true;
					continue;
				}

				return false;
			}

			return true;
		}

		static bool IsReservedWord(string varExpr)
		{
			// TODO: investigate doing this like Rhino does (switch on length check first letter or two)
			switch (varExpr)
			{
				// literals
				case "null":
				case "false":
				case "true":

				// ES5 Keywords
				case "break":
				case "case":
				case "catch":
				case "continue":
				case "debugger":
				case "default":
				case "delete":
				case "do":
				case "else":
				case "finally":
				case "for":
				case "function":
				case "if":
				case "in":
				case "instanceof":
				case "new":
				case "return":
				case "switch":
				case "this":
				case "throw":
				case "try":
				case "typeof":
				case "var":
				case "void":
				case "while":
				case "with":

				// ES5 Future Reserved Words
				case "abstract":
				case "boolean":
				case "byte":
				case "char":
				case "class":
				case "const":
				case "double":
				case "enum":
				case "export":
				case "extends":
				case "final":
				case "float":
				case "goto":
				case "implements":
				case "import":
				case "int":
				case "interface":
				case "long":
				case "native":
				case "package":
				case "private":
				case "protected":
				case "public":
				case "short":
				case "static":
				case "super":
				case "synchronized":
				case "throws":
				case "transient":
				case "volatile":

				// ES5 Possible Reserved Words
				case "let":
				case "yield":
				{
					return true;
				}
				default:
				{
					return false;
				}
			}
		}

		/// <summary>
		/// Trivial conversion method. Essentially performs a cast.
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		/// <remarks>
		/// Supports conversion via System.Web.UI.PropertyConverter.ObjectFromString(Type, MemberInfo, string)
		/// </remarks>
		public static EcmaScriptIdentifier Parse(string value)
		{
			return new EcmaScriptIdentifier(value);
		}

		#endregion Methods

		#region Operators

		/// <summary>
		/// Implicit type conversion allows to be used directly as a String
		/// </summary>
		/// <param name="ident">valid ECMAScript identifier</param>
		/// <returns></returns>
		public static implicit operator string(EcmaScriptIdentifier ident)
		{
			if (ident == null)
			{
				return string.Empty;
			}

			return ident._identifier;
		}

		/// <summary>
		/// Implicit type conversion allows to be used directly with Strings
		/// </summary>
		/// <param name="ident">valid ECMAScript identifier</param>
		/// <returns></returns>
		public static implicit operator EcmaScriptIdentifier(string ident)
		{
			return new EcmaScriptIdentifier(ident);
		}

		#endregion Operators

		#region IJsonSerializable Members

		void IJsonSerializable.ReadJson(JsonReader reader)
		{
			throw new NotImplementedException("The method or operation is not implemented.");
		}

		void IJsonSerializable.WriteJson(JsonWriter writer)
		{
			if (string.IsNullOrEmpty(_identifier))
			{
				writer.TextWriter.Write("null");
			}
			else
			{
				// TODO: determine if this should output parens around identifier
				writer.TextWriter.Write(_identifier);
			}
		}

		#endregion IJsonSerializable Members

		#region Object Overrides

		/// <summary>
		/// Compares the identifiers.
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public override bool Equals(object obj)
		{
			EcmaScriptIdentifier that = obj as EcmaScriptIdentifier;
			if (that == null)
			{
				return base.Equals(obj);
			}

			if (string.IsNullOrEmpty(_identifier) && string.IsNullOrEmpty(that._identifier))
			{
				// null and String.Empty are equivalent
				return true;
			}

			return StringComparer.Ordinal.Equals(_identifier, that._identifier);
		}

		/// <summary>
		/// Returns the identifier.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return _identifier;
		}

		/// <summary>
		/// Returns the hash code for this identifier.
		/// </summary>
		/// <returns></returns>
		public override int GetHashCode()
		{
			if (_identifier == null)
			{
				return 0;
			}

			return _identifier.GetHashCode();
		}

		#endregion Object Overrides
	}
}
#endif
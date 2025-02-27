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
using System.Reflection;

namespace DLD.JsonFx
{
	/// <summary>
	/// Specifies the name of the property which specifies if member should be serialized.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
	public class JsonSpecifiedPropertyAttribute : Attribute
	{
		#region Fields

		string _specifiedProperty;

		#endregion Fields

		#region Init

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="propertyName">the name of the property which controls serialization for this member</param>
		public JsonSpecifiedPropertyAttribute(string propertyName)
		{
			_specifiedProperty = propertyName;
		}

		#endregion Init

		#region Properties

		/// <summary>
		/// Gets and sets the name of the property which
		/// specifies if member should be serialized
		/// </summary>
		public string SpecifiedProperty
		{
			get => _specifiedProperty;
			set => _specifiedProperty = value;
		}

		#endregion Properties

		#region Methods

		/// <summary>
		/// Gets the name specified for use in Json serialization.
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static string GetJsonSpecifiedProperty(MemberInfo memberInfo)
		{
			if (Equals(memberInfo, null))
				//!memberInfo.IsDefined (typeof(JsonSpecifiedPropertyAttribute),true))
				//!Attribute.IsDefined (memberInfo, typeof(JsonSpecifiedPropertyAttribute)))
			{
				return null;
			}

#if WINDOWS_STORE
			var attribute = memberInfo.GetCustomAttribute<JsonSpecifiedPropertyAttribute> (true);
#else
			var attribute =
				GetCustomAttribute(memberInfo, typeof(JsonSpecifiedPropertyAttribute)) as JsonSpecifiedPropertyAttribute;
#endif
			return attribute != null ? attribute.SpecifiedProperty : null;
		}

		#endregion Methods
	}
}
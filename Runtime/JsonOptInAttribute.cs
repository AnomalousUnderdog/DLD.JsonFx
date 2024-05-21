using System;

namespace DLD.JsonFx
{
	/// <summary>
	/// Specifies that members of this class that should be serialized must be explicitly specified.
	/// Classes that this attribute is applied to need to explicitly
	/// declare every member that should be serialized with the JsonMemberAttribute.
	/// <seealso cref="JsonMemberAttribute"/>
	/// </summary>
	public class JsonOptInAttribute : Attribute
	{
	}

	public class JsonUseTypeHintAttribute : Attribute
	{
	}
}
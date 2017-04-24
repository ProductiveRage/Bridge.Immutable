# (Bridge.NET) ProductiveRage.Immutable
A way to make it easier to create and work with immutable classes in [Bridge.NET](http://bridge.net/) - I want to be able to write immutable classes, such as:

	public class PersonDetails
	{
		public PersonDetails(int id, NameDetails name)
		{
			if (name == null)
				throw new ArgumentNullException("name");

			Id = id;
			Name = name;
		}

		public int Id { get; }
		public NameDetails Name { get; }

		public PersonDetails WithId(int id)
		{
			return (id == Id) ? this : new PersonDetails(id, Name);
		}
		public PersonDetails WithName(NameDetails name)
		{
			if (name == null)
				throw new ArgumentNullException("name");
			return (name == Name) ? this : new PersonDetails(Id, name);
		}
	}
	
But I'm sick of how much work it is.

The "With{whatever}" methods explode the classes, but they're important so that if I have a reference such as -
	
	var p = new PersonDetails(1, new NameDetails("Joseph"));

.. and I want to get a new **PersonDetails** reference with a new Name value, I don't want to have to write -

	p = new PersonDetails(p.Id, new NameDetails("Joe"));
	
While it's not *too* bad in that case, because there are only two constructor arguments, many data types will have more than two. And if one of these classes got an extra constructor argument added in the future then *every* update statement such as that would need to be changed to account for the new argument. The "WithName" method avoids that by allowing the following alternative approach:
	
	p = p.WithName(new NameDetails("Joe"));
	
But this adds more weight to each class.

I could try to code-gen them, but I don't think that there's an amazing solution for that available at this time. So I'm trying something different. Possibly a bit crazy.

What this library does is allow you to write this:
	
	public class PersonDetails : IAmImmutable
	{
		public PersonDetails(int id, NameDetails name)
		{
			this.CtorSet(_ => _.Id, id);
			this.CtorSet(_ => _.Name, name);
		}
		public int Id { get; }
		public NameDetails Name { get; }
	}
	
"CtorSet" is an extension method which will set the specified property on the "this" instance to the specified value. It's type safe in that the type of the property must match the type of the value, so you can't accidentally set the (string) "Name" property using the (int) "id" constructor argument. "CtorSet" does some reflection work to identify what property to target and will call the setter even though it is private - as such, this should only ever be done from within the constructor, otherwise you could set private properties of classes all over the place and then they wouldn't be immutable any more! Once a property has been set with "CtorSet", it may not be set again on that instance (a runtime exception will occur) - this means that, so long as all properties are set in the constructor then that instance's data will be "frozen". This is also why the "CtorSet" method will only work on types that implement the **IAmImmutable** interface - this is an empty interface, so there is no burden in implementing it, it exists only to identify classes as having been designed for "CtorSet" to operate on them.

The other extension method in the library is "With", which saves you from having to write "With{whatever}" methods for every property. You can now write:

	p = p.With(_ => _.Name, new NameDetails("Joe"));

.. and use the same property-matching logic as "CtorSet" supports. (Note that the "With" extension method includes a check that the new value is different from the current value - if they're the same then "With" returns back the original reference; there's no point creating a new one in that case).

### Pro-Tip: Type *even* less by relying on the library's code fix

When you start writing a class that implements IAmImmutable, you can save yourself some key presses by only writing out the class and an empty constructor - you will then see the quick actions light bulb (presuming you're using Visual Studio 2015 or later) appear beside the constructor. The menu will offer to "Populate class from constructor", which will fill out the body of the constructor and declare the public properties.

![IAmImmutable class auto-population quick action option](http://www.productiverage.com/Content/Images/ProductiveRage.Immutable.AutoFix2.png)

## No more implicit nulls!

A decision that I made in writing this code was that I wanted to reduce the number of "if-null-throw" conditions that I had in my class constructors, so "CtorSet" applies that automatically. You may not pass any null argument into a constructor that will use "CtorSet" to set the property value.

This is because I believe that null should be an "opt-in" approach and, to try to make steps towards that, I'm disallowing null in "CtorSet" calls and requiring all optional values to be wrapped in an **Optional&lt;T&gt;** struct, which is also included in the library.

So, if you wanted a property that may or may not have a value then you would write a class such as this (somewhat contrived example):
	
	public class PersonDetails : IAmImmutable
	{
		public PersonDetails(int id, NameDetails name, Optional<PersonDetails> reportsTo)
		{
			this.CtorSet(_ => _.Id, id);
			this.CtorSet(_ => _.Name, name);
			this.CtorSet(_ => _.ReportsTo, reportsTo);
		}
		public int Id { get; }
		public NameDetails Name { get; }
		public Optional<PersonDetails> ReportsTo { get; }
	}
	
You can be confident that "Name" will never be null, but "ReportsTo" *may* have no value - but that's ok, because the type system tells you that it's optional!

**Optional&lt;T&gt;** is a simple struct with properties such as

	static Optional<T> Missing { get; }
	
	bool IsDefined { get; }
	T Value { get; }
	
and helper methods such as

	T GetValueOrDefault(T defaultValue);
	
There's also an implicit operator so that any "T" can be implicitly cast to **Optional&lt;T&gt;**. So in the example above, we could create a new instance with no "ReportsTo" value with either:

	var p = new PersonDetails(1, new NameDetails("Joseph"), Optional<PersonDetails>.Missing);
	
.. or with:
	
	var p = new PersonDetails(1, new NameDetails("Joseph"), null);

If we *did* have a "ReportsTo" value to set then we could write:

	var p = new PersonDetails(1, new NameDetails("Joseph"), theBoss);

It's got equality-handling built in, so two **Optional&lt;int&gt;** instances will match if they either both are without values or if they both have values and both have the *same* value - eg.

	var i0 = Optional.For(1);
	var i1 = Optional.For(1);
	var areEqual = (i0 == i1); // True!
	
This also demonstrates the generic static function "For" that may be used when you definitely, positively *do* need to create an Optional (note that C#'s type inference means that you don't have to specify the type of "T" when calling "For"). All of the following do the same:

	var i0 = Optional.For(1);
	var i1 = new Optional<int>(1);
	var i2 = (Optional<int>)1;

I can't take too much credit for this part of the code since I started from the "[Optional](https://github.com/AArnott/ImmutableObjectGraph/blob/f1b0e44ea472d2d31423a54a695d9cdd3b3ca510/src/ImmutableObjectGraph/Optional%601.cs)" struct in Andrew Arnott's [ImmutableObjectGraph](https://github.com/AArnott/ImmutableObjectGraph) repo, which is another project that has a similar goal - though for regular C#, rather than Bridge. It's also coming to the end (I believe) of an upheaval, moving from using T4 templates to Roslyn. Should be interesting! (Information accurate as of 12th December 2015).

## NonNullLists

Finally, there is also a simple list-like type in the library; **NonNullList&lt;T&gt;**. This is an immutable collection of objects (using a linked list internally to allow sharing of data between instances where possible, in order to avoid throwing even more work at the GC than necessary). It *also* will not accept any null values - if a collection must support not having values for some of the elements then it should be of type **NonNullList&lt;Optional&lt;T&gt;&gt;**.

It currently only has a minimal interface of:

	static NonNullList<T> Empty { get; }
	
	uint Count { get; }
	T this[uint index] { get; }
	NonNullList<T> SetValue(uint index, T value)
	NonNullList<T> Insert(T item); // Inserts at the start of the list
	
It implements **IEnumerable&lt;T&gt;** so that it will play nicely with other code.

And there's another static helper function "Of" -

	var items = NonNullList.Of("One", "Two", "Three");
	
Again, C#'s type inference means that you don't need to specify the type when you call "Of". But also note that null references are still not acceptable here. If you wanted to declare a list of strings that may or may not contain null references then you would have to do something like

	var items = NonNullList.Of<Optional<string>>("One", "Two", "Three");
	
or

	var items = NonNullList.Of(Optional.For("One"), Optional.For("Two"), Optional.For("Three"));

There is a "With" extension method that makes working with sets a little easier. If you have a class such as:

	public class PersonDetails : IAmImmutable
	{
		public PersonDetails(int id, NameDetails name, NonNullList<PersonDetails> staff)
		{
			this.CtorSet(_ => _.Id, id);
			this.CtorSet(_ => _.Name, name);
			this.CtorSet(_ => _.Staff, staff);
		}
		public int Id { get; }
		public NameDetails Name { get; }
		public NonNullList<PersonDetails> Staff { get; }
	}

.. and you wanted to update the third entry in an instance's "Staff" set, then you could do something like the following:

	p = p.With(_ => _.Staff, 2, joe);

## Caveats

This is still early days. I might decide in a couple of months that this was an interesting experiment but ultimately not something to continue. Maybe an awesome code-gen option will come to light and I'll prefer that. Maybe other people I work with will be unwilling to accept this sort of code because it looks "weird" with its property identifier lambdas.

Right now, though, I'm excited about how this makes writing immutable classes easier *and* I had fun writing the sort-of reflection code for the JavaScript that Bridge.NET generates (Bridge v2 will have full support for reflection but the release schedule, as of December 2015, is still pending - this library is for Bridge 1.x).

~~However, there *is* one thing that I don't like. If you don't use the expected property identifier format -~~

	// This is all good if Id is a property
	this.CtorSet(_ => _.Id, id);
	
	// This is bad but will, unfortunately, compile
	this.CtorSet(_ => _.Id + 1, id);
	
~~.. then you'll get a runtime exception. Likewise if the property that is specified does not have both a setter and a getter (a private getter and/or setter is fine but *no* getter/setter is a problem). If the getter or setter use one of Bridge's attributes that change the names of functions in the JavaScript then that too will result in a runtime exception (such as the [[Name]](http://bridge.net/kb/attribute-reference/#Name) or [[Template]](http://bridge.net/kb/attribute-reference/#Template) attributes).~~

The reason that I like immutability is that it gives me guarantees about data. And the reason that I like strongly-typed code is that I like to know sooner, rather than later, when code has been written that will try to break these sorts of guarantees. Having the compiler alert me to a broken rule is much preferable to a runtime exception ~~- and these property identifiers only being validated at runtime is less-than-ideal in my eyes. I have great hope, however, that a  Roslyn-based Analyser will be able to ensure that things are written as expected *and pick up on it at compile-time, before the code is ever executed*. And that is my next step.~~

**Update:** This solution now comes with its own Roslyn-based Analysers (which are distributed with the NuGet package) that addresses this problem! The Analysers look at your source code that makes "CtorSet" or "With" calls and ensures that the property Identifier lambda meets the requirements. Now the following *will* get identified as an error -

	// Identified as an error by the Analyser:
	//
	//   "CtorSet's propertyRetriever lambda must directly indicate an instance property with
	//    a getter and a setter (which may be private)"
	//
	// The project will not build if you're using Visual Studio 2015
	//
	this.CtorSet(_ => _.Id + 1, id);

Similarly, invalid "With" calls will prevent the build from completing -

	// Error:
	//
	//   "With's propertyRetriever lambda must directly indicate an instance property with a
	//    getter and a setter (which may be private)"
	//
	p = p.With(_ => _.ToString(), "value");

## Visual Studio Support
The NuGet package build from this solution ([nuget.org/packages/ProductiveRage.Immutable](https://www.nuget.org/packages/ProductiveRage.Immutable)) will work in earlier versions of Visual Studio but in order to build the package from source you need Visual Studio 2015 Update 1.

The Analysers included in the solution will require Visual Studio 15 to take advantage of them when consuming the NuGet package (Update 1 is *not* required). Visual Studio 2013 will not be able to use the Analysers, of course, but the library itself will work.

# BsonUtility
[![license](https://img.shields.io/github/license/Alex-Rachel/BsonUtility.svg)](https://github.com/Alex-Rachel/BsonUtility/blob/main/LICENSE) 
[![last](https://img.shields.io/github/last-commit/Alex-Rachel/BsonUtility.svg)](https://github.com/Alex-Rachel/BsonUtility)
[![issue](https://img.shields.io/github/issues/Alex-Rachel/BsonUtility.svg)](https://github.com/Alex-Rachel/BsonUtility/issues)
[![topLanguage](https://img.shields.io/github/languages/top/Alex-Rachel/BsonUtility.svg)](https://github.com/Alex-Rachel/BsonUtility)


BsonUtility for Unity

A fast BSON library for Unity, compliant to the whole BSON specification test suite. The library parses the binary data on-demand, delaying copies until the last second.

BSON is parsed and generated as specified for version 1.1 of the [BSON specification](http://bsonspec.org/spec.html).

## Basic Usage

Create Documents using Dictionary Literals:

```csharp
public class BsonTest
{
    class MyClass
    {
        public bool b = true;
        
        public int i = 100;
        
        public string s = "Hello World";
        
        public List<A> a = new List<A>();
        
        public class A
        {
            public int a = 100;
            
            public string b = "Hello World";
        }
    }
    
    private void Test()
    {
        MyClass myClass = new MyClass();

        // To Bytes
        var bytes = Bson.ToBson(myClass);

        // To Objects
        var myClass2 = Bson.ToObject<MyClass>(bytes);
    }
}
```

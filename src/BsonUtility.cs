using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace BsonUtility
{
    /// <summary>
    /// Bson Utility.
    /// </summary>
    public sealed class Bson
    {
        private static readonly Dictionary<byte[], object> BsonObjects = new(ByteArrayComparer.Default);

        private static readonly IDictionary<Type, IList<PropertyMeta>> PropertyMetas = new Dictionary<Type, IList<PropertyMeta>>();

        private static readonly IDictionary<Type, ArrayMeta> ArrayMetas = new Dictionary<Type, ArrayMeta>();

        private static readonly IDictionary<Type, ObjectMeta> ObjectMetas = new Dictionary<Type, ObjectMeta>();

        public static byte[] ToBson(object obj, bool cache = false)
        {
            if (obj == null)
            {
                return null;
            }

            using Writer writer = new Writer();
            writer.Encode(obj);
            return writer.Bytes;
        }

        public static void ToBson(object obj, Writer writer)
        {
            if (obj == null || writer == null)
            {
                return;
            }

            writer.Encode(obj);
        }

        public static T ToObject<T>(byte[] bson)
        {
            if (bson == null || bson.Length == 0)
            {
                return default;
            }

            if (BsonObjects.TryGetValue(bson, out var obj) && obj != null)
            {
                return (T)obj;
            }

            using Reader reader = new Reader(bson);
            return (T)reader.Decode(typeof(T));
        }

        public static T ToObject<T>(Reader reader)
        {
            return reader == null ? default : (T)reader.Decode(typeof(T));
        }

        public static void Clear()
        {
            BsonObjects.Clear();
            PropertyMetas.Clear();
            ArrayMetas.Clear();
            ObjectMetas.Clear();
        }

        private static IList<PropertyMeta> AddPropertyMetas(Type type)
        {
            if (PropertyMetas.TryGetValue(type, out var propertyMetaList1) && propertyMetaList1 != null)
            {
                return propertyMetaList1;
            }
            IList<PropertyMeta> propertyMetaList2 = new List<PropertyMeta>();
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.Name != "Item")
                {
                    PropertyMeta propertyMeta = new PropertyMeta()
                    {
                        Info = property,
                        IsField = false
                    };
                    propertyMetaList2.Add(propertyMeta);
                }
            }

            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                PropertyMeta propertyMeta = new PropertyMeta()
                {
                    Info = field,
                    IsField = true
                };
                propertyMetaList2.Add(propertyMeta);
            }

            PropertyMetas[type] = propertyMetaList2;
            return propertyMetaList2;
        }

        private static ArrayMeta AddArrayMeta(Type type)
        {
            if (ArrayMetas.TryGetValue(type, out var arrayMeta1))
            {
                return arrayMeta1;
            }

            ArrayMeta arrayMeta2 = new ArrayMeta
            {
                IsArray = type.IsArray
            };
            if (type.GetInterface("System.Collections.IList") != null)
            {
                arrayMeta2.IsList = true;
            }

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.Name == "Item")
                {
                    ParameterInfo[] indexParameters = property.GetIndexParameters();
                    if (indexParameters.Length == 1)
                    {
                        arrayMeta2.ItemType = indexParameters[0].ParameterType == typeof(int) ? property.PropertyType : typeof(object);
                    }
                }
            }

            ArrayMetas[type] = arrayMeta2;
            return arrayMeta2;
        }

        private static ObjectMeta AddObjectMeta(System.Type type)
        {
            if (ObjectMetas.TryGetValue(type, out var objectMeta1))
            {
                return objectMeta1;
            }

            ObjectMeta objectMeta2 = new ObjectMeta();
            if (type.GetInterface("System.Collections.IDictionary") != null)
            {
                objectMeta2.IsDict = true;
            }

            objectMeta2.Properties = new Dictionary<string, PropertyMeta>();
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.Name == "Item")
                {
                    ParameterInfo[] indexParameters = property.GetIndexParameters();
                    if (indexParameters.Length == 1)
                    {
                        objectMeta2.ElemType = indexParameters[0].ParameterType == typeof(string) ? property.PropertyType : typeof(object);
                    }
                }
                else
                {
                    PropertyMeta propertyMeta = new PropertyMeta()
                    {
                        Info = property,
                        Type = property.PropertyType
                    };
                    objectMeta2.Properties.Add(property.Name, propertyMeta);
                }
            }

            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                PropertyMeta propertyMeta = new PropertyMeta()
                {
                    Info = field,
                    IsField = true,
                    Type = field.FieldType
                };
                objectMeta2.Properties.Add(field.Name, propertyMeta);
            }

            ObjectMetas[type] = objectMeta2;
            return objectMeta2;
        }

        private enum ValueType : byte
        {
            Bool = 1,
            SByte = 2,
            Byte = 3,
            Char = 4,
            Short = 5,
            UShort = 6,
            Int = 7,
            UInt = 8,
            Int64 = 9,
            UInt64 = 10, // 0x0A
            Decimal = 11, // 0x0B
            Float = 12, // 0x0C
            Double = 13, // 0x0D
            DateTime = 14, // 0x0E
            Enum = 15, // 0x0F
            String = 16, // 0x10
            Array = 17, // 0x11
            Vector2 = 18, // 0x12
            Vector3 = 19, // 0x13
            Vector4 = 20, // 0x14
            Color = 21, // 0x15
            Color32 = 22, // 0x16
            Quaternion = 23, // 0x17
            Bounds = 24, // 0x18
            Rect = 25, // 0x19
            Matrix = 26, // 0x1A
            Object = 27, // 0x1B
        }

        private struct PropertyMeta
        {
            public MemberInfo Info { get; set; }

            public bool IsField { get; set; }

            public Type Type { get; set; }
        }

        private struct ArrayMeta
        {
            public Type ItemType { get; set; }

            public bool IsArray { get; set; }

            public bool IsList { get; set; }
        }

        private struct ObjectMeta
        {
            public Type ElemType { get; set; }

            public bool IsDict { get; set; }

            public IDictionary<string, PropertyMeta> Properties { get; set; }
        }

        private sealed class ByteArrayComparer : IEqualityComparer<byte[]>
        {
            public static ByteArrayComparer Default => new ByteArrayComparer();

            public bool Equals(byte[] left, byte[] right)
            {
                if (left == null || right == null)
                {
                    return left == right;
                }

                if (left == right)
                {
                    return true;
                }

                if (left.Length != right.Length)
                {
                    return false;
                }

                for (int index = 0; index < left.Length; ++index)
                {
                    if (left[index] != right[index])
                    {
                        return false;
                    }
                }

                return true;
            }

            public int GetHashCode(byte[] obj)
            {
                if (obj == null)
                {
                    throw new ArgumentNullException(nameof(obj));
                }

                int num1 = 0;
                int num2 = 0;
                foreach (byte num3 in obj)
                {
                    num1 += num3;
                    num2 += num1;
                }

                return num1 ^ num2;
            }
        }

        public sealed class Reader : IDisposable
        {
            private readonly MemoryStream _stream;
            private readonly BinaryReader _reader;
            private bool _isDisposed;

            public Reader(byte[] bson) : this(new MemoryStream(bson))
            {
            }

            public Reader(MemoryStream stream)
            {
                _stream = stream ?? new MemoryStream();
                _reader = new BinaryReader(_stream);
            }

            public object Decode(Type type)
            {
                Type type1 = Nullable.GetUnderlyingType(type);
                if (type1 == null)
                {
                    type1 = type;
                }

                Type type2 = type1;
                switch ((ValueType)_reader.ReadByte())
                {
                    case ValueType.Bool:
                        if (type2 != typeof(bool))
                            throw new Exception($"Bson type is bool, but expect {type2}");
                        return _reader.ReadBoolean();
                    case ValueType.SByte:
                        if (type2 != typeof(sbyte))
                            throw new Exception($"Bson type is sbyte, but expect {type2}");
                        return _reader.ReadSByte();
                    case ValueType.Byte:
                        if (type2 != typeof(byte))
                            throw new Exception($"Bson type is byte, but expect {type2}");
                        return _reader.ReadByte();
                    case ValueType.Char:
                        if (type2 != typeof(char))
                            throw new Exception($"Bson type is char, but expect {type2}");
                        return _reader.ReadChar();
                    case ValueType.Short:
                        if (type2 != typeof(short))
                            throw new Exception($"Bson type is short, but expect {type2}");
                        return _reader.ReadInt16();
                    case ValueType.UShort:
                        if (type2 != typeof(ushort))
                            throw new Exception($"Bson type is ushort, but expect {type2}");
                        return _reader.ReadUInt16();
                    case ValueType.Int:
                        if (type2 != typeof(int))
                            throw new Exception($"Bson type is int, but expect {type2}");
                        return _reader.ReadInt32();
                    case ValueType.UInt:
                        if (type2 != typeof(uint))
                            throw new Exception($"Bson type is uint, but expect {type2}");
                        return _reader.ReadUInt32();
                    case ValueType.Int64:
                        if (type2 != typeof(long))
                            throw new Exception($"Bson type is long, but expect {type2}");
                        return _reader.ReadInt64();
                    case ValueType.UInt64:
                        if (type2 != typeof(ulong))
                            throw new Exception($"Bson type is ulong, but expect {type2}");
                        return _reader.ReadUInt64();
                    case ValueType.Decimal:
                        if (type2 != typeof(Decimal))
                            throw new Exception($"Bson type is decimal, but expect {type2}");
                        return _reader.ReadDecimal();
                    case ValueType.Float:
                        if (type2 != typeof(float))
                            throw new Exception($"Bson type is float, but expect {type2}");
                        return _reader.ReadSingle();
                    case ValueType.Double:
                        if (type2 != typeof(double))
                            throw new Exception($"Bson type is double, but expect {type2}");
                        return _reader.ReadDouble();
                    case ValueType.DateTime:
                        if (type2 != typeof(DateTime))
                            throw new Exception($"Bson type is DateTime, but expect {type2}");
                        return ReadDateTime();
                    case ValueType.Enum:
                        if (!type2.IsEnum)
                            throw new Exception($"Bson type is Enum, but expect {type2}");
                        return ReadEnum(type2);
                    case ValueType.String:
                        if (type2 != typeof(string))
                            throw new Exception($"Bson type is string, but expect {type2}");
                        return _reader.ReadString();
                    case ValueType.Array:
                        if (!type2.IsArray && type2.GetInterface("System.Collections.IList") == null)
                            throw new Exception($"Bson type is Array, but expect {type2}");
                        return ReadArray(type2);
                    case ValueType.Vector2:
                        if (type2 != typeof(Vector2))
                            throw new Exception($"Bson type is vector2, but expect {type2}");
                        return ReadVector2();
                    case ValueType.Vector3:
                        if (type2 != typeof(Vector3))
                            throw new Exception($"Bson type is vector3, but expect {type2}");
                        return ReadVector3();
                    case ValueType.Vector4:
                        if (type2 != typeof(Vector4))
                            throw new Exception($"Bson type is vector4, but expect {type2}");
                        return ReadVector4();
                    case ValueType.Color:
                        if (type2 != typeof(Color))
                            throw new Exception($"Bson type is color, but expect {type2}");
                        return ReadColor();
                    case ValueType.Color32:
                        if (type2 != typeof(Color32))
                            throw new Exception($"Bson type is color32, but expect {type2}");
                        return ReadColor32();
                    case ValueType.Quaternion:
                        if (type2 != typeof(Quaternion))
                            throw new Exception($"Bson type is quaternion, but expect {type2}");
                        return ReadQuaternion();
                    case ValueType.Bounds:
                        if (type2 != typeof(Bounds))
                            throw new Exception($"Bson type is bounds, but expect {type2}");
                        return ReadBounds();
                    case ValueType.Rect:
                        if (type2 != typeof(Rect))
                            throw new Exception($"Bson type is rect, but expect {type2}");
                        return ReadRect();
                    case ValueType.Matrix:
                        if (type2 != typeof(Matrix4x4))
                            throw new Exception($"Bson type is matrix4x4, but expect {type2}");
                        return ReadMatrix();
                    case ValueType.Object:
                        return ReadObject(type2);
                    default:
                        return null;
                }
            }

            private DateTime ReadDateTime()
            {
                return new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) + new TimeSpan(_reader.ReadInt64() * 10000L);
            }

            private object ReadEnum(Type type) => Enum.ToObject(type, _reader.ReadInt32());

            private object ReadArray(Type type)
            {
                ArrayMeta arrayMeta = AddArrayMeta(type);
                IList list;
                Type type1;
                if (arrayMeta.IsArray)
                {
                    list = new ArrayList();
                    type1 = type.GetElementType();
                }
                else
                {
                    list = (IList)Activator.CreateInstance(type);
                    type1 = arrayMeta.ItemType;
                }

                long num = _reader.ReadInt32() + _reader.BaseStream.Position;
                while (_reader.BaseStream.Position < num)
                {
                    object obj = Decode(type1);
                    list.Add(obj);
                }

                object obj1;
                if (arrayMeta.IsArray)
                {
                    // ReSharper disable once AssignNullToNotNullAttribute
                    obj1 = Array.CreateInstance(type1, list.Count);
                    for (int index = 0; index < list.Count; ++index)
                    {
                        ((Array)obj1).SetValue(list[index], index);
                    }
                }
                else
                {
                    obj1 = list;
                }

                return obj1;
            }

            private Vector2 ReadVector2()
            {
                return new Vector2
                {
                    x = _reader.ReadSingle(),
                    y = _reader.ReadSingle()
                };
            }

            private Vector3 ReadVector3()
            {
                return new Vector3
                {
                    x = _reader.ReadSingle(),
                    y = _reader.ReadSingle(),
                    z = _reader.ReadSingle()
                };
            }

            private Vector4 ReadVector4()
            {
                return new Vector4
                {
                    x = _reader.ReadSingle(),
                    y = _reader.ReadSingle(),
                    z = _reader.ReadSingle(),
                    w = _reader.ReadSingle()
                };
            }

            private Color ReadColor()
            {
                return new Color
                {
                    r = _reader.ReadSingle(),
                    g = _reader.ReadSingle(),
                    b = _reader.ReadSingle(),
                    a = _reader.ReadSingle()
                };
            }

            private Color32 ReadColor32()
            {
                return new Color32
                {
                    r = _reader.ReadByte(),
                    g = _reader.ReadByte(),
                    b = _reader.ReadByte(),
                    a = _reader.ReadByte()
                };
            }

            private Quaternion ReadQuaternion()
            {
                return new Quaternion
                {
                    x = _reader.ReadSingle(),
                    y = _reader.ReadSingle(),
                    z = _reader.ReadSingle(),
                    w = _reader.ReadSingle()
                };
            }

            private Bounds ReadBounds()
            {
                return new Bounds
                {
                    center = new Vector3
                    {
                        x = _reader.ReadSingle(),
                        y = _reader.ReadSingle(),
                        z = _reader.ReadSingle()
                    },
                    extents = new Vector3
                    {
                        x = _reader.ReadSingle(),
                        y = _reader.ReadSingle(),
                        z = _reader.ReadSingle()
                    }
                };
            }

            private Rect ReadRect()
            {
                return new Rect
                {
                    x = _reader.ReadSingle(),
                    y = _reader.ReadSingle(),
                    width = _reader.ReadSingle(),
                    height = _reader.ReadSingle()
                };
            }

            private Matrix4x4 ReadMatrix()
            {
                return new Matrix4x4
                {
                    m00 = _reader.ReadSingle(),
                    m10 = _reader.ReadSingle(),
                    m20 = _reader.ReadSingle(),
                    m30 = _reader.ReadSingle(),
                    m01 = _reader.ReadSingle(),
                    m11 = _reader.ReadSingle(),
                    m21 = _reader.ReadSingle(),
                    m31 = _reader.ReadSingle(),
                    m02 = _reader.ReadSingle(),
                    m12 = _reader.ReadSingle(),
                    m22 = _reader.ReadSingle(),
                    m32 = _reader.ReadSingle(),
                    m03 = _reader.ReadSingle(),
                    m13 = _reader.ReadSingle(),
                    m23 = _reader.ReadSingle(),
                    m33 = _reader.ReadSingle()
                };
            }

            private object ReadObject(Type type)
            {
                ObjectMeta objectMeta = AddObjectMeta(type);
                object instance = Activator.CreateInstance(type);
                long num = _reader.ReadInt32() + _reader.BaseStream.Position;
                while (_reader.BaseStream.Position < num)
                {
                    string key = _reader.ReadString();
                    if (objectMeta.Properties.TryGetValue(key, out var propertyMeta))
                    {
                        object obj = Decode(propertyMeta.Type);
                        if (propertyMeta.IsField)
                        {
                            ((FieldInfo)propertyMeta.Info).SetValue(instance, obj);
                        }
                        else
                        {
                            PropertyInfo info = (PropertyInfo)propertyMeta.Info;
                            if (info.CanWrite)
                            {
                                info.SetValue(instance, obj, null);
                            }
                        }
                    }
                    else
                    {
                        object obj = Decode(objectMeta.ElemType);
                        if (objectMeta.IsDict)
                        {
                            ((IDictionary)instance).Add(key, obj);
                        }
                        else
                        {
                            throw new Exception($"The type {type} doesn't have the property '{key}'");
                        }
                    }
                }

                return instance;
            }

            public void Dispose()
            {
                if (_isDisposed)
                {
                    return;
                }

                _reader?.Close();
                _stream?.Dispose();
                _isDisposed = true;
            }
        }

        public sealed class Writer : IDisposable
        {
            private readonly MemoryStream _stream;
            private readonly BinaryWriter _writer;
            private bool _isDisposed;

            public byte[] Bytes
            {
                get
                {
                    if (_stream == null)
                    {
                        return null;
                    }
                    byte[] destinationArray = new byte[_stream.Length];
                    Array.Copy(_stream.GetBuffer(), destinationArray, destinationArray.Length);
                    return destinationArray;
                }
            }

            public Writer(MemoryStream stream = null)
            {
                _stream = stream ?? new MemoryStream();
                _writer = new BinaryWriter(_stream);
            }

            public void Encode(object obj)
            {
                switch (obj)
                {
                    case null:
                        throw new Exception("Obj is null, encode return.");
                    case bool flag:
                        Write(flag);
                        break;
                    case sbyte num1:
                        Write(num1);
                        break;
                    case byte num2:
                        Write(num2);
                        break;
                    case char ch:
                        Write(ch);
                        break;
                    case short num3:
                        Write(num3);
                        break;
                    case ushort num4:
                        Write(num4);
                        break;
                    case int num5:
                        Write(num5);
                        break;
                    case uint num6:
                        Write(num6);
                        break;
                    case long num7:
                        Write(num7);
                        break;
                    case ulong num8:
                        Write(num8);
                        break;
                    case Decimal num9:
                        Write(num9);
                        break;
                    case float num10:
                        Write(num10);
                        break;
                    case double num11:
                        Write(num11);
                        break;
                    case DateTime dateTime:
                        Write(dateTime);
                        break;
                    case Enum _:
                        WriteEnum(obj);
                        break;
                    case string str:
                        Write(str);
                        break;
                    case IList list:
                        Write(list);
                        break;
                    case IDictionary dictionary:
                        Write(dictionary);
                        break;
                    case Vector2 vector2:
                        Write(vector2);
                        break;
                    case Vector3 vector3:
                        Write(vector3);
                        break;
                    case Vector4 vector4:
                        Write(vector4);
                        break;
                    case Color color:
                        Write(color);
                        break;
                    case Color32 color32:
                        Write(color32);
                        break;
                    case Quaternion quaternion:
                        Write(quaternion);
                        break;
                    case Bounds bounds:
                        Write(bounds);
                        break;
                    case Rect rect:
                        Write(rect);
                        break;
                    case Matrix4x4 matrix4X4:
                        Write(matrix4X4);
                        break;
                    default:
                        Write(obj);
                        break;
                }
            }

            private void Write(bool value)
            {
                _writer.Write((byte)ValueType.Bool);
                _writer.Write(value);
            }

            private void Write(sbyte value)
            {
                _writer.Write((byte)ValueType.SByte);
                _writer.Write(value);
            }

            private void Write(byte value)
            {
                _writer.Write((byte)ValueType.Byte);
                _writer.Write(value);
            }

            private void Write(char value)
            {
                _writer.Write((byte)ValueType.Char);
                _writer.Write(value);
            }

            private void Write(short value)
            {
                _writer.Write((byte)ValueType.Short);
                _writer.Write(value);
            }

            private void Write(ushort value)
            {
                _writer.Write((byte)ValueType.UShort);
                _writer.Write(value);
            }

            private void Write(int value)
            {
                _writer.Write((byte)ValueType.Int);
                _writer.Write(value);
            }

            private void Write(uint value)
            {
                _writer.Write((byte)ValueType.UInt);
                _writer.Write(value);
            }

            private void Write(long value)
            {
                _writer.Write((byte)ValueType.Int64);
                _writer.Write(value);
            }

            private void Write(ulong value)
            {
                _writer.Write((byte)ValueType.UInt64);
                _writer.Write(value);
            }

            private void Write(Decimal value)
            {
                _writer.Write((byte)ValueType.Decimal);
                _writer.Write(value);
            }

            private void Write(float value)
            {
                _writer.Write((byte)ValueType.Float);
                _writer.Write(value);
            }

            private void Write(double value)
            {
                _writer.Write((byte)ValueType.Double);
                _writer.Write(value);
            }

            private void Write(DateTime value)
            {
                TimeSpan timeSpan = value.Kind == DateTimeKind.Local
                    ? value - new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).ToLocalTime()
                    : value - new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                _writer.Write((byte)ValueType.DateTime);
                _writer.Write(timeSpan.TotalSeconds * 1000L);
            }

            private void WriteEnum(object value)
            {
                _writer.Write((byte)ValueType.Enum);
                _writer.Write((int)value);
            }

            private void Write(string value)
            {
                _writer.Write((byte)ValueType.String);
                _writer.Write(value);
            }

            private void Write(IList value)
            {
                using Writer writer = new Writer();
                foreach (object obj in value)
                {
                    if (obj != null)
                        writer.Encode(obj);
                }

                int length = (int)writer._stream.Length;
                _writer.Write((byte)ValueType.Array);
                _writer.Write(length);
                _writer.Write(writer._stream.GetBuffer(), 0, length);
            }

            private void Write(IDictionary value)
            {
                using Writer writer = new Writer();
                foreach (DictionaryEntry dictionaryEntry in value)
                {
                    if (dictionaryEntry.Value != null)
                    {
                        writer._writer.Write(dictionaryEntry.Key.ToString());
                        writer.Encode(dictionaryEntry.Value);
                    }
                }

                int length = (int)writer._stream.Length;
                _writer.Write((byte)ValueType.Object);
                _writer.Write(length);
                _writer.Write(writer._stream.GetBuffer(), 0, length);
            }

            private void Write(Vector2 value)
            {
                _writer.Write((byte)ValueType.Vector2);
                _writer.Write(value.x);
                _writer.Write(value.y);
            }

            private void Write(Vector3 value)
            {
                _writer.Write((byte)ValueType.Vector3);
                _writer.Write(value.x);
                _writer.Write(value.y);
                _writer.Write(value.z);
            }

            private void Write(Vector4 value)
            {
                _writer.Write((byte)ValueType.Vector4);
                _writer.Write(value.x);
                _writer.Write(value.y);
                _writer.Write(value.z);
                _writer.Write(value.w);
            }

            private void Write(Color value)
            {
                _writer.Write((byte)ValueType.Color);
                _writer.Write(value.r);
                _writer.Write(value.g);
                _writer.Write(value.b);
                _writer.Write(value.a);
            }

            private void Write(Color32 value)
            {
                _writer.Write((byte)ValueType.Color32);
                _writer.Write(value.r);
                _writer.Write(value.g);
                _writer.Write(value.b);
                _writer.Write(value.a);
            }

            private void Write(Quaternion value)
            {
                _writer.Write((byte)ValueType.Quaternion);
                _writer.Write(value.x);
                _writer.Write(value.y);
                _writer.Write(value.z);
                _writer.Write(value.w);
            }

            private void Write(Bounds value)
            {
                _writer.Write((byte)ValueType.Bounds);
                _writer.Write(value.center.x);
                _writer.Write(value.center.y);
                _writer.Write(value.center.z);
                _writer.Write(value.extents.x);
                _writer.Write(value.extents.y);
                _writer.Write(value.extents.z);
            }

            private void Write(Rect value)
            {
                _writer.Write((byte)ValueType.Rect);
                _writer.Write(value.x);
                _writer.Write(value.y);
                _writer.Write(value.width);
                _writer.Write(value.height);
            }

            private void Write(Matrix4x4 value)
            {
                _writer.Write((byte)ValueType.Matrix);
                _writer.Write(value.m00);
                _writer.Write(value.m10);
                _writer.Write(value.m20);
                _writer.Write(value.m30);
                _writer.Write(value.m01);
                _writer.Write(value.m11);
                _writer.Write(value.m21);
                _writer.Write(value.m31);
                _writer.Write(value.m02);
                _writer.Write(value.m12);
                _writer.Write(value.m22);
                _writer.Write(value.m32);
                _writer.Write(value.m03);
                _writer.Write(value.m13);
                _writer.Write(value.m23);
                _writer.Write(value.m33);
            }

            private void Write(object value)
            {
                using Writer writer = new Writer();
                foreach (PropertyMeta addPropertyMeta in AddPropertyMetas(value.GetType()))
                {
                    if (addPropertyMeta.IsField)
                    {
                        object obj = ((FieldInfo)addPropertyMeta.Info).GetValue(value);
                        if (obj != null)
                        {
                            writer._writer.Write(addPropertyMeta.Info.Name);
                            writer.Encode(obj);
                        }
                    }
                    else
                    {
                        PropertyInfo info = (PropertyInfo)addPropertyMeta.Info;
                        if (info.CanRead && info.CanWrite)
                        {
                            object obj = info.GetValue(value, null);
                            if (obj != null)
                            {
                                writer._writer.Write(addPropertyMeta.Info.Name);
                                writer.Encode(obj);
                            }
                        }
                    }
                }

                int length = (int)writer._stream.Length;
                _writer.Write((byte)27);
                _writer.Write(length);
                _writer.Write(writer._stream.GetBuffer(), 0, length);
            }

            public void Dispose()
            {
                if (_isDisposed)
                {
                    return;
                }

                _writer?.Close();
                _stream?.Dispose();
                _isDisposed = true;
            }
        }
    }
}
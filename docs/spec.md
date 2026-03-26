# Bytery

Bytery is a **schema-aware binary serialization format and library** designed to replace JSON in **transport** and **storage** scenarios.

Its goal is simple: **make structured data much smaller and much faster to encode and decode** than plain JSON.

In benchmark scenarios with realistic nested business objects, Bytery reduced payload size to **~20% of raw JSON** while also delivering much faster encode and decode times. When combined with **GZIP**, the final payload dropped to around **10% to 15% of the original JSON size** in the tests shown below.

Unlike JSON, Bytery is built for compact binary transport from the start. It uses a structured wire format with features such as:

* schema-based object encoding
* string and date tables
* compact integer representations
* null-aware primitive encodings
* optional outer GZIP compression

The result is a format focused on **efficiency**, **determinism**, and **low overhead**, while still preserving the original data without loss.

## Benchmark snapshot

The following results come from tests using realistic nested payloads rather than tiny synthetic examples.

### NormalRegular

Realistic business-like nested objects: Clients -> Purchases -> Items -> Product + Address/Shipping/flags/preferences. Randomized but deterministic (seed).

| Actor              | Output Size |        Encode |         Decode |          Total |
| ------------------ | ----------: | ------------: | -------------: | -------------: |
| Newtonsoft         |     1.12 MB |     8656.7 ms |      8125.1 ms |     16781.8 ms |
| Newtonsoft + GZIP  |   212.15 KB |     9351.5 ms |      8877.2 ms |     18228.6 ms |
| Bytery + GZIP      |   176.74 KB |      898.4 ms |       204.4 ms |      1102.9 ms |
| **Bytery vs JSON** |   **15.5%** | **Enc 9.64x** | **Dec 39.74x** | **Tot 15.22x** |

### NormalHeavy

Heavy realistic payload: larger Clients dataset with Purchases/Items, Payment/Shipping histories, device profiles, balances, multiple addresses, and richer metadata. Randomized but deterministic (seed + fixed baseUtc).

| Actor              | Output Size |        Encode |        Decode |         Total |
| ------------------ | ----------: | ------------: | ------------: | ------------: |
| Newtonsoft         |    10.48 MB |    14042.3 ms |    13955.0 ms |    27997.4 ms |
| Newtonsoft + GZIP  |     1.84 MB |    13460.2 ms |    13036.2 ms |    26496.4 ms |
| Bytery + GZIP      |     1.21 MB |     2480.2 ms |     2405.7 ms |     4885.9 ms |
| **Bytery vs JSON** |   **11.5%** | **Enc 5.66x** | **Dec 5.80x** | **Tot 5.73x** |



> Benchmark results depend on payload shape, repetition patterns, runtime, hardware, and comparison method. These numbers should be treated as representative results from the current test suite, not as a universal guarantee for every dataset.

## How it works

First, it is important to understand what Bytery actually is.

**Bytery is not just an implementation. It is a binary serialization format with its own wire protocol.**  
This repository is one implementation of that format.

The size and performance gains come from addressing several limitations of plain JSON as a transport and storage format.

For example, JSON is text-based:

- numbers, booleans, and null values are represented as text and must be parsed from characters
- JSON has no native date type, so dates must also be stored as text
- parsers must process quotes, commas, braces, escapes, and separators to reconstruct values
- repeated strings may appear hundreds or thousands of times in the same payload
- repeated object structures (**schemas**) must also repeat the same field names over and over again

Bytery takes a different approach.

At the protocol level, it includes several optimizations:

- it uses a binary format, so values can be read directly in their native representation instead of being parsed from text
- it stores repeated strings in a **string table**, allowing later occurrences to be written as small pointers
- it stores repeated dates in a **date table**, reducing duplication for common timestamp values
- variable-size values carry their size in the first byte(s), so the decoder knows how many bytes to read without scanning for delimiters
- fixed-size values such as booleans and floating-point numbers can be read directly with minimal overhead
- it caches object **schemas**, so object with the same schemas don't need to repeat field names and type information for every object

These are some of the core ideas that make Bytery much more efficient than raw JSON.  
The following sections explain each part of the format in more detail.

## Payload structure

A Bytery payload has the following high-level structure, always encoded as raw binary data:

```text
[magic][version][zmsk][present zones...]
```

The `zmsk` byte declares which top-level zones are present in the payload.

For container v1, the canonical zone order is:

```text
[header][files][string-table][date-table][schema-table][data]
```

Zones are never reordered.
If a zone is absent, it is simply omitted from the payload.

### `[magic]`

Every Bytery payload starts with the 4-byte magic:

```text
BYT1 = 0x42 0x59 0x54 0x31
```

This identifies the payload as a Bytery container.

### `[version]`

The magic is immediately followed by **1 byte** containing the container version.

For example, version `1` is encoded as:

```text
0x01
```

### `[zmsk]`

The version byte is immediately followed by **1+ byte** containing the first zone mask (`ZMSK`).

For v1, this byte declares which zones are present in the payload.

So the first 6 bytes of a Bytery v1 payload are always:

```text
[0x42][0x59][0x54][0x31][0x01][zmsk]
```

### `[header]`

The header zone is optional.

It stores key-value pairs that are not part of the main data itself, but describe the payload or carry extra metadata about the file.

### `[files]`

The files zone is optional.

It stores file entries associated with the payload, where each entry contains a file name and a raw byte payload.

When present, the zone also carries its total byte length, so a decoder can skip the whole files body in one jump if needed.

### `[string-table]`

The string table stores cached strings used by the payload.

String values in the data section, as well as schema field names, may point to this table instead of writing the same text repeatedly.

### `[date-table]`

The date table stores cached date values used by the payload.

Date values in the data section may point to this table instead of writing the same date multiple times.

### `[schema-table]`

The schema table stores the schemas used by the payload.

Objects in the data section point to a schema in this table. That schema defines which fields the object has, their types, and how the object body must be read.

### `[data]`

The data zone stores the actual payload value.

It may contain primitive values, arrays, maps, and objects. It can also reference entries from the string table, date table, and schema table.

We will examine each part in detail later.
Before that, we first need to understand how the data types work.

## The byte vector

A Bytery payload is nothing more than a **sequence of bytes**.

Each byte has a value from **0 to 255**, and the decoder reads the payload by moving a cursor through that byte sequence.

In many parts of the protocol, the **current byte works as a tag**.  
That tag tells the decoder how the next bytes must be interpreted.

This is one of the most important ideas in Bytery: **the meaning of the next bytes is usually defined by the current byte**.

## Bytery data types

Bytery has its own internal data types.  
Each one is designed for a specific purpose, usually related to **compact storage**, **fast decoding**, **pointer-based reuse**, and **low-overhead null handling**.

At a high level, Bytery types fall into two categories:

- **Literal**: the value is stored directly in the payload. In other words, **the bytes you read are the value itself**.
- **Dynamic**: the value may be stored either as a literal value, as a pointer to a cache or table, or as a null marker, depending on the current byte.

## Extended unsigned integer encodings

Before looking at the main Bytery types, it is useful to define the **unsigned integer encodings** used by several parts of the protocol.

These are not standalone Bytery types.  
They are building blocks used inside other encodings.  
In all cases, you must read the next bytes and combine them into a single unsigned integer value.

- **UIntB8**: a compact unsigned integer encoded as a base value plus the next byte  
  `value = base + nextByte`

- **UInt16BE**: a 16-bit unsigned integer  
  `value = byte1 * 256 + byte2`

- **UInt24BE**: a 24-bit unsigned integer  
  `value = byte1 * 256^2 + byte2 * 256 + byte3`

- **UInt32BE**: a 32-bit unsigned integer  
  `value = byte1 * 256^3 + byte2 * 256^2 + byte3 * 256 + byte4`

- **UInt40BE**: a 40-bit unsigned integer  
  `value = byte1 * 256^4 + byte2 * 256^3 + byte3 * 256^2 + byte4 * 256 + byte5`

- **UInt48BE**: a 48-bit unsigned integer  
  `value = byte1 * 256^5 + byte2 * 256^4 + byte3 * 256^3 + byte4 * 256^2 + byte5 * 256 + byte6`

- **UInt56BE**: a 56-bit unsigned integer  
  `value = byte1 * 256^6 + byte2 * 256^5 + byte3 * 256^4 + byte4 * 256^3 + byte5 * 256^2 + byte6 * 256 + byte7`

- **UInt64BE**: a 64-bit unsigned integer  
  `value = byte1 * 256^7 + byte2 * 256^6 + byte3 * 256^5 + byte4 * 256^4 + byte5 * 256^3 + byte6 * 256^2 + byte7 * 256 + byte8`

For all `UInt*BE` variants, **big-endian** means the most significant byte comes first.

## LUINT - Literal Unsigned Integer

**LUINT** is the Bytery type used to represent a **non-negative integer value**.

It is mainly used for lengths, counts, indexes, table positions and other protocol metadata.

When decoding a LUINT, you first read the current byte and interpret it as follows:

- `0..246`: if the byte value is between `0..246` inclusive, it is the value itself. For example, the current byte value is 157, that means the LUInt value is 157.
- `247`: if the value is 247, it is a B8 UInteger with Base=247, so you need to read the next byte and sum with 247. Example: `[247, 100, ...]`, the value is `247 + 100 = 347`
- `248..254`: if the value is between `248..254` inclusive, it is an `UInt16BE to UInt64BE`, and you need to read and compute the next bytes as shown in the Extended Unsigned Integers section
- `255`: **NULL**. If the value of the byte is 255, that means a **NULL** value for the LUInt

So, LUINT is a compact integer encoding where small values cost fewer bytes, while larger values automatically expand to the required width.

## LINT - Literal Signed Integer

**LINT** is the Bytery type used to represent a **signed integer value**.

It is mainly used for integer values in the payload itself, and unlike `LUINT`, it can represent negative values.

When decoding a LINT, you first read the current byte and interpret it as follows:

- `0..219`: if the byte value is between `0..219` inclusive, it is a positive literal value. For example, if the current byte is `157`, then the LINT value is `157`.

- `220..238`: if the byte value is between `220..238` inclusive, it is a negative literal value from `-1..-19`.  
  The formula is: `value = -(currentByte - 219)`  
  Example: if the current byte is `220`, then the value is `-(220 - 219) = -1`

- `239`: if the current byte is `239`, it is a compact positive integer with base `220`, so you must read the next byte and sum it with `220`.  
  Example: `[239, 100, ...]`, the value is `220 + 100 = 320`

- `240..246`: if the byte value is between `240..246` inclusive, it is a positive `UInt16BE` to `UInt64BE`, and you must read and compute the next bytes as shown in the Extended Unsigned Integer Encodings section

- `247`: if the current byte is `247`, it is a compact negative integer with base `20`, so you must read the next byte and apply the formula:  
  `value = -(20 + nextByte)`  
  Example: `[247, 5, ...]`, the value is `-(20 + 5) = -25`

- `248..254`: if the byte value is between `248..254` inclusive, it is a negative `UInt16BE` to `UInt64BE`, and you must read and compute the next bytes as an unsigned magnitude, then apply the negative sign to the final value

- `255`: **NULL**. If the value of the byte is `255`, that means a **NULL** value for the LINT

So, LINT is a compact signed integer encoding where small positive and negative values cost fewer bytes, while larger magnitudes automatically expand to the required width.

## LSTR - Literal String

**LSTR** is the Bytery type used to represent a **literal UTF-8 string value**.

It is mainly used in places where the protocol must store the string itself directly, without using a pointer.

An `LSTR` is encoded as:

```text
[LUINT byteLengthOrNull][UTF-8 bytes]
```

When decoding a LSTR, you first read a `LUINT` value and interpret it as follows:

* `0..254`: if the `LUINT` is not null, its value is the **UTF-8 byte length** of the string, so you must read that many bytes and decode them as UTF-8 text
* `255`: **NULL**. If the `LUINT` is null, that means the LSTR value is **NULL**

Important: the length of a string in Bytery is always measured in **UTF-8 bytes**, not in characters.

For example:

* an empty string is encoded as a `LUINT` with value `0`, followed by no bytes
* the string `"hello"` is encoded as a `LUINT` with value `5`, followed by the UTF-8 bytes of `"hello"`

So, LSTR is a simple literal string encoding: first the UTF-8 byte length, then the UTF-8 bytes themselves.

## DSTR - Dynamic String

**DSTR** is the Bytery type used to represent a **dynamic UTF-8 string value**.

Unlike `LSTR`, a `DSTR` can represent:

* a literal UTF-8 string
* a pointer to a string in the **string table**
* a **NULL** value

This makes `DSTR` much more compact when the same string appears many times in the same payload.

When decoding a DSTR, you first read the current byte and interpret it as follows:

* `0..156`: if the byte value is between `0..156` inclusive, it is the **UTF-8 byte length** of the string, so you must read that many bytes and decode them as UTF-8 text

* `157..246`: if the byte value is between `157..246` inclusive, it is a **literal pointer** to the string table for indexes `0..89`
  The formula is: `stringTableIndex = currentByte - 157`
  Example: if the current byte is `157`, then the string table index is `0`

* `247`: if the current byte is `247`, it is a compact literal string length with base `157`, so you must read the next byte and sum it with `157`
  Example: `[247, 10, ...]`, the string byte length is `157 + 10 = 167`

* `248..250`: if the byte value is between `248..250` inclusive, it is a literal string length encoded as `UInt16BE` to `UInt32BE`, and you must read and compute the next bytes as shown in the Extended Unsigned Integer Encodings section

* `251`: if the current byte is `251`, it is a compact pointer to the string table with base `90`, so you must read the next byte and sum it with `90`
  Example: `[251, 10, ...]`, the string table index is `90 + 10 = 100`

* `252..254`: if the byte value is between `252..254` inclusive, it is a string table pointer encoded as `UInt16BE` to `UInt32BE`, and you must read and compute the next bytes as shown in the Extended Unsigned Integer Encodings section

* `255`: **NULL**. If the value of the byte is `255`, that means a **NULL** value for the DSTR

If the DSTR is a literal string, the decoder reads the UTF-8 bytes directly from the payload.
If the DSTR is a pointer, the decoder uses the computed index to load the correct value from the **string table**.

Important: just like `LSTR`, all DSTR literal lengths are measured in **UTF-8 bytes**, not in characters.

So, DSTR is a flexible string encoding that can store the string itself or reuse a previous value through the string table, depending on what is more efficient.

## BOOL - Boolean

**BOOL** is the Bytery type used to represent a **boolean value**.

It is a fixed-size type encoded in exactly **1 byte**.

When decoding a BOOL, you read the current byte and interpret it as follows:

- `0`: **False**
- `1`: **True**
- `2`: **NULL**

So, BOOL is a very compact boolean encoding where `False`, `True`, and `NULL` are all represented in a single byte.

## FLOAT4BYTES - 4-Byte Floating-Point Number

**FLOAT4BYTES** is the Bytery type used to represent a **4-byte floating-point value**.

It uses the IEEE 754 single-precision binary format in **big-endian** order for non-null values.

When decoding a FLOAT4BYTES value, you read the next bytes and interpret them as follows:

- if the first 2 bytes are `0xFF 0xFF`, the value is **NULL**
- otherwise, those 2 bytes are the beginning of a normal IEEE 754 single-precision payload, so you must read **2 more bytes** and interpret the full 4-byte sequence as a normal **single-precision floating-point value**

So, the following payload:

```text
0xFF 0xFF
```

means:

```text
NULL
```

Important: FLOAT4BYTES does not use a separate tag or length prefix.

Its wire form is:

* **NULL** => exactly **2 bytes**: `0xFF 0xFF`
* **non-null** => exactly **4 bytes**: normal IEEE 754 single-precision payload in big-endian order

This works because `0xFF 0xFF` already starts a NaN pattern, so it can be safely reserved as the null sentinel.

## FLOAT8BYTES - 8-Byte Floating-Point Number

**FLOAT8BYTES** is the Bytery type used to represent an **8-byte floating-point value**.

It uses the IEEE 754 double-precision binary format in **big-endian** order for non-null values.

When decoding a FLOAT8BYTES value, you read the next bytes and interpret them as follows:

* if the first 2 bytes are `0xFF 0xFF`, the value is **NULL**
* otherwise, those 2 bytes are the beginning of a normal IEEE 754 double-precision payload, so you must read **6 more bytes** and interpret the full 8-byte sequence as a normal **double-precision floating-point value**

So, the following payload:

```text
0xFF 0xFF
```

means:

```text
NULL
```

Important: FLOAT8BYTES does not use a separate tag or length prefix.

Its wire form is:

* **NULL** => exactly **2 bytes**: `0xFF 0xFF`
* **non-null** => exactly **8 bytes**: normal IEEE 754 double-precision payload in big-endian order

This works because `0xFF 0xFF` already starts a NaN pattern, so it can be safely reserved as the null sentinel.

For both FLOAT4BYTES and FLOAT8BYTES, **big-endian** means the most significant byte comes first.

## LDATE - Literal Date

**LDATE** is the Bytery type used to represent a **literal date value**.

It is a fixed-size type encoded in exactly **8 bytes**, using the UTC `DateTime.Ticks` value in **big-endian** order.

An `LDATE` is used only inside the **date table**.  
It does not use pointers, tags, or length prefixes. The value is always stored directly as its 8-byte UTC ticks representation.

So, when decoding an LDATE, you simply read the next **8 bytes** and interpret them as a UTC ticks value.

Important: the ticks must represent a valid UTC `DateTime` value.

For LDATE, **big-endian** means the most significant byte comes first.

## DDATE - Dynamic Date

**DDATE** is the Bytery type used to represent a **dynamic date value**.

Unlike `LDATE`, a `DDATE` can represent:

- a literal UTC ticks value stored directly in the payload
- a pointer to a date in the **date table**
- a **NULL** value

This makes `DDATE` more efficient when the same date appears many times in the same payload.

When decoding a DDATE, you first read the current byte and interpret it as follows:

- `0x00..0x2B`: if the byte value is between `0x00..0x2B` inclusive, it is the **first byte of the UTC ticks value**, so you must read this byte plus the next 7 bytes and interpret the 8-byte sequence as a big-endian UTC ticks value

- `0x2C..0xFA`: if the byte value is between `0x2C..0xFA` inclusive, it is a **literal pointer** to the date table for indexes `0..206`  
  The formula is: `dateTableIndex = currentByte - 0x2C`  
  Example: if the current byte is `0x2C`, then the date table index is `0`

- `0xFB`: if the current byte is `0xFB`, it is a compact pointer to the date table with base `207`, so you must read the next byte and sum it with `207`  
  Example: `[0xFB, 5, ...]`, the date table index is `207 + 5 = 212`

- `0xFC..0xFE`: if the byte value is between `0xFC..0xFE` inclusive, it is a date table pointer encoded as `UInt16BE` to `UInt32BE`, and you must read and compute the next bytes as shown in the Extended Unsigned Integer Encodings section

- `0xFF`: **NULL**. If the value of the byte is `0xFF`, that means a **NULL** value for the DDATE

If the DDATE is a literal value, the decoder reads the full 8-byte UTC ticks value directly from the payload.  
If the DDATE is a pointer, the decoder uses the computed index to load the correct value from the **date table**.

Important: the inline literal form is possible because valid UTC ticks always start with a first byte in the range `0x00..0x2B`, which leaves the remaining byte range available for pointer and null markers.

So, DDATE is a flexible date encoding that can store the date itself or reuse a previous value through the date table, depending on what is more efficient.

## SOBJ - Session Object

**SOBJ** is the Bytery type used to represent an **object slot**.

It is used when a schema field has base type **Object**.

Unlike primitive types, an object slot may need more than one representation.  
Depending on the current byte, an `SOBJ` can represent:

- an object that uses the **expected schema**
- an object that uses a **different schema**
- a **primitive scalar override**
- a **primitive array override**
- an **object array override**
- a **NULL** value

When decoding an SOBJ, you first read the current byte and interpret it as follows:

- `0`: the object is present and uses the **expected schema** for that field

- `1..7`: the value is a **primitive scalar override**, using the Bytery base type id directly  
  The following bytes must be decoded using that primitive type  
  The base type ids are:  
  `1 = Integer`  
  `2 = FLOAT4BYTES`  
  `3 = FLOAT8BYTES`  
  `4 = Boolean`  
  `5 = Date`  
  `6 = String`  
  `7 = Bytes`

- `129..135`: the value is a **primitive array override**  
  These values are `0x80 | baseTypeId`, which means `ArrayFlag | baseTypeId`  
  Example: `129 = Integer[]`, `134 = String[]`

- `136`: the value is an **object array override** using the expected object schema for each element

- `252`: the value is an object with a **schema override**, and the schema index is stored in the next **1 byte**

- `253`: the value is an object with a **schema override**, and the schema index is stored in the next **2 bytes** as `UInt16BE`

- `254`: the value is an object with a **schema override**, and the schema index is stored in the next **3 bytes** as `UInt24BE`

- `255`: **NULL**. If the value of the byte is `255`, that means a **NULL** value for the SOBJ

If the SOBJ uses the expected schema, the decoder immediately reads the object body using that schema.

If the SOBJ uses a schema override, the decoder first reads the schema index, loads the correct schema from the **schema table**, and then reads the object body using that schema.

If the SOBJ uses a primitive or array override, the decoder reads the following bytes using the overridden type instead of an object schema.

Important: an object scalar override is **not** represented by base type id `8`.  
If a different object schema is needed, Bytery uses the explicit schema override forms `252..254`.

So, SOBJ is the mechanism that allows object fields to remain flexible while still keeping the main payload compact and schema-driven.

## BARR - Byte Array

**BARR** is the Bytery type used to represent a **byte array value**.

It is encoded as:

```text
[LUINT byteLengthOrNull][raw bytes]
```

When decoding a BARR, you first read a `LUINT` value and interpret it as follows:

* `0..254`: if the `LUINT` is not null, its value is the **byte length** of the array, so you must read that many raw bytes
* `255`: **NULL**. If the `LUINT` is null, that means the BARR value is **NULL**

Important: unlike strings, a BARR value is not decoded as UTF-8 text.
The bytes are read exactly as they are.

For example:

* an empty byte array is encoded as a `LUINT` with value `0`, followed by no bytes
* a byte array with length `5` is encoded as a `LUINT` with value `5`, followed by the next 5 raw bytes
* a null byte array is encoded as a `LUINT` null marker

So, BARR is a simple byte-array encoding: first the byte length, then the raw bytes themselves.

## SMAT - Schema Type

**SMAT** is the Bytery type used to represent the **schema header** of an entry in the **schema table**.

It is not a data value by itself.  
Instead, it tells the decoder what kind of schema is being described by the current schema-table entry, and how the remaining bytes of that schema entry must be read.

Depending on the current byte, an `SMAT` can represent:

- a **NULL schema**: this means the schema-table entry is null

```text
[SMAT_NULL]
```

* an **object schema**: a schema that represents an object with a collection of fields

```text
[SMAT_OBJECT_FIELD_COUNT][FIELDS]*
```

Each field is encoded as:

```text
[fieldType][fieldName:DSTR][schemaPointer if baseType = Object]
```

* a **map schema**: a schema that represents `Map<String, T>`

```text
[SMAT_MAP_TYPE][schemaPointer if T = Object]
```

* a **map-of-arrays schema**: a schema that represents `Map<String, Array<T>>`

```text
[SMAT_MAP_ARRAY_TYPE][schemaPointer if T = Object]
```

* an **array schema**: a schema that represents `Array<T>`

```text
[SMAT_ARRAY_TYPE][schemaPointer if T = Object]
```

So, `SMAT` is always the first byte of a schema-table entry, and that first byte defines both:

* the **kind** of schema
* the **layout** of the remaining bytes of that schema entry

## Interpreting the first byte

This is how to interpret the first byte of an `SMAT`:

* `255`: **NULL schema**. This schema-table entry is null

* `247..254`: **array schema**
  These values represent `Array<T>` schemas, where the element type is given directly by the tag

* `239..246`: **map-of-arrays schema**
  These values represent `Map<String, Array<T>>` schemas, where the map value type is an array

* `231..238`: **map schema**
  These values represent `Map<String, T>` schemas, where the map value type is not an array

* `0..229`: **object schema with literal field count**
  If the current byte is between `0..229` inclusive, it means the schema is an object schema, and the byte value itself is the number of fields in that object

* `230`: **object schema with extended field count**
  If the current byte is `230`, the schema is an object schema, and the field count must be read next as a `LUINT`

The base type ids used by `SMAT` are:

* `1 = Integer`
* `2 = FLOAT4BYTES`
* `3 = FLOAT8BYTES`
* `4 = Boolean`
* `5 = Date`
* `6 = String`
* `7 = Bytes`
* `8 = Object`

## Array schema tags

If the `SMAT` value is between `247..254`, it represents an `Array<T>` schema.

The mapping is:

* `247 = Integer[]`
* `248 = FLOAT4BYTES[]`
* `249 = FLOAT8BYTES[]`
* `250 = Boolean[]`
* `251 = Date[]`
* `252 = String[]`
* `253 = Bytes[]`
* `254 = Object[]`

An array schema entry is encoded as:

```text
[SMAT][objectSchemaPointer if T = Object]
```

If the element type is not `Object`, the schema entry ends at the `SMAT` byte itself.

If the element type is `Object`, the schema entry must also include a pointer to another schema-table entry, so the decoder knows which object schema must be used for each array element.

## Map schema tags

If the `SMAT` value is between `231..238`, it represents a `Map<String, T>` schema.

The mapping is:

* `231 = Map<String, Integer>`
* `232 = Map<String, FLOAT4BYTES>`
* `233 = Map<String, FLOAT8BYTES>`
* `234 = Map<String, Boolean>`
* `235 = Map<String, Date>`
* `236 = Map<String, String>`
* `237 = Map<String, Bytes>`
* `238 = Map<String, Object>`

A map schema entry is encoded as:

```text
[SMAT][objectSchemaPointer if T = Object]
```

If the map value type is not `Object`, the schema entry ends at the `SMAT` byte itself.

If the map value type is `Object`, the schema entry must also include a pointer to another schema-table entry, so the decoder knows which object schema must be used for each map value.

## Map-of-arrays schema tags

If the `SMAT` value is between `239..246`, it represents a `Map<String, Array<T>>` schema.

The mapping is:

* `239 = Map<String, Integer[]>`
* `240 = Map<String, FLOAT4BYTES[]>`
* `241 = Map<String, FLOAT8BYTES[]>`
* `242 = Map<String, Boolean[]>`
* `243 = Map<String, Date[]>`
* `244 = Map<String, String[]>`
* `245 = Map<String, Bytes[]>`
* `246 = Map<String, Object[]>`

A map-of-arrays schema entry is encoded as:

```text
[SMAT][objectSchemaPointer if T = Object]
```

If the array element type is not `Object`, the schema entry ends at the `SMAT` byte itself.

If the array element type is `Object`, the schema entry must also include a pointer to another schema-table entry, so the decoder knows which object schema must be used for each array element inside the map value.

## Object schema entries

If the `SMAT` value is between `0..229`, the value itself is the number of fields in the object schema.

If the `SMAT` value is `230`, the field count must be read next as a `LUINT`.

So, an object schema entry is encoded as:

```text
[SMAT or SMAT+LUINT fieldCount][field1][field2][field3]...
```

Each field is encoded as:

```text
[fieldType][fieldName][objectSchemaPointer if baseType = Object]
```

### `fieldType`

The `fieldType` is **1 byte** and uses the Bytery field type ids directly.

Scalar field types:

* `1 = Integer`
* `2 = FLOAT4BYTES`
* `3 = FLOAT8BYTES`
* `4 = Boolean`
* `5 = Date`
* `6 = String`
* `7 = Bytes`
* `8 = Object`

Array field types use the same base type ids with `ArrayFlag = 0x80`:

* `129 = Integer[]`
* `130 = FLOAT4BYTES[]`
* `131 = FLOAT8BYTES[]`
* `132 = Boolean[]`
* `133 = Date[]`
* `134 = String[]`
* `135 = Bytes[]`
* `136 = Object[]`

So, the field type byte tells the decoder both:

* the base type of the field
* whether the field is a scalar or an array

### `fieldName`

The field name is encoded as a `DSTR`.

This means the field name may be:

* stored literally as UTF-8 text
* stored as a pointer to the **string table**
* stored as **NULL** (although field names are expected to resolve to valid strings in normal schemas)

This makes repeated field names very compact across the schema table.

### `objectSchemaPointer`

If the field base type is `Object`, whether scalar or array, the field must also include a pointer to another schema-table entry.

This pointer tells the decoder which schema must be used for that object field.

So, these field types require an object schema pointer after the field name:

* `8 = Object`
* `136 = Object[]`

All other field types do **not** include an object schema pointer.

## Object schema pointer encoding

Whenever an `SMAT` entry needs to point to another schema-table entry, it uses the schema-pointer family:

* `252`: schema index stored in the next **1 byte**
* `253`: schema index stored in the next **2 bytes** as `UInt16BE`
* `254`: schema index stored in the next **3 bytes** as `UInt24BE`

So, an object schema pointer is encoded as:

```text
[252][indexU8]
```

or

```text
[253][indexUInt16BE]
```

or

```text
[254][indexUInt24BE]
```

Important: this is **not** a `LUINT`.
Schema pointers use their own dedicated pointer family.

## Summary

So, `SMAT` is the first byte of every schema-table entry, and it tells the decoder:

* whether the entry is null, object, map, map-of-arrays, or array
* which base type is involved
* whether extra bytes must be read
* whether object-schema pointers are required
* how many fields an object schema contains

In other words, `SMAT` is the entry point for decoding every schema in the **schema table**.

## SMAT examples

The examples below show complete **schema-table entries** using `SMAT`.

These are only examples to make the structure easier to understand.  
The schema pointer indexes used here are arbitrary example values.

### Example 1: NULL schema

A null schema entry is just:

```text
[255]
```

Meaning:

* `255` = NULL schema

---

### Example 2: Array schema

Schema:

```text
Array<Integer>
```

Encoded as:

```text
[247]
```

Meaning:

* `247` = `Integer[]`

This schema entry ends immediately, because `Integer` is not `Object`.

---

### Example 3: Map schema

Schema:

```text
Map<String, Object> using schema #5
```

Encoded as:

```text
[238][252][5]
```

Meaning:

* `238` = `Map<String, Object>`
* `[252][5]` = schema pointer to schema-table entry `5`

So this means:

```text
Map<String, Object(schema #5)>
```

---

### Example 4: Map-of-arrays schema

Schema:

```text
Map<String, Integer[]>
```

Encoded as:

```text
[239]
```

Meaning:

* `239` = `Map<String, Integer[]>`

This schema entry ends immediately, because the array element type is not `Object`.

---

### Example 5: Object schema

Schema:

```text
{
  id: Integer,
  name: String,
  age: Integer
}
```

Encoded as:

```text
[3]
  [1][2]["i""d"]
  [6][4]["n""a""m""e"]
  [1][3]["a""g""e"]
```

Expanded in bytes:

```text
[3]
[1][2][0x69][0x64]
[6][4][0x6E][0x61][0x6D][0x65]
[1][3][0x61][0x67][0x65]
```

Meaning:

* `[3]` = object schema with **3 fields**

First field:

* `[1]` = `Integer`
* `[2]["i""d"]` = field name `"id"`

Second field:

* `[6]` = `String`
* `[4]["n""a""m""e"]` = field name `"name"`

Third field:

* `[1]` = `Integer`
* `[3]["a""g""e"]` = field name `"age"`

So this schema describes:

```text
{ id: Integer, name: String, age: Integer }
```

---

### Example 6: Nested object schema tree

Consider the following data shape:

```text
client{
  id: 1,
  name: "John",
  age: 30,
  pet: {
    id: 3,
    name: "Whiskers",
    age: 3
  }
}
```

This requires two schemas:

* one schema for `pet`
* one schema for `client`, which points to the `pet` schema

#### Pet schema

Schema:

```text
{
  id: Integer,
  name: String,
  age: Integer
}
```

Encoded as schema-table entry `#3`:

```text
[3]
  [1][2]["i""d"]
  [6][4]["n""a""m""e"]
  [1][3]["a""g""e"]
```

Expanded in bytes:

```text
[3]
[1][2][0x69][0x64]
[6][4][0x6E][0x61][0x6D][0x65]
[1][3][0x61][0x67][0x65]
```

#### Client schema

Schema:

```text
{
  id: Integer,
  name: String,
  age: Integer,
  pet: Object using schema #3
}
```

Encoded as schema-table entry `#4`:

```text
[4]
  [1][2]["i""d"]
  [6][4]["n""a""m""e"]
  [1][3]["a""g""e"]
  [8][3]["p""e""t"][252][3]
```

Expanded in bytes:

```text
[4]
[1][2][0x69][0x64]
[6][4][0x6E][0x61][0x6D][0x65]
[1][3][0x61][0x67][0x65]
[8][3][0x70][0x65][0x74][252][3]
```

Meaning:

* `[4]` = object schema with **4 fields**

The first three fields are:

* `id: Integer`
* `name: String`
* `age: Integer`

The fourth field is:

* `[8]` = `Object`
* `[3]["p""e""t"]` = field name `"pet"`
* `[252][3]` = schema pointer to schema-table entry `3`

So this schema describes:

```text
{
  id: Integer,
  name: String,
  age: Integer,
  pet: Object(schema #3)
}
```

This means the decoder reads `client.pet` using the `pet` schema.

---

### Summary

These examples show the main shapes of `SMAT` entries:

* `[255]` for a null schema
* `[247..254]` for array schemas
* `[231..238]` for map schemas
* `[239..246]` for map-of-arrays schemas
* `[0..229]` or `[230][LUINT]` for object schemas

They also show that:

* simple schemas can be represented with **just 1 byte**
* object-based schemas may require a **schema pointer**
* object fields also require a **schema pointer**
* field names are stored as `DSTR`
* object schemas can describe nested schema trees with very few bytes

See how much `SMAT` helps compact schema definitions: some complete schemas can be represented with only **1 byte**, such as simple arrays and maps, while more complex schemas can still be represented with very few bytes, as in the nested `client` tree above.

## Payload sections

Now that the main Bytery data types have been introduced, we can look at the actual sections that compose a full Bytery payload.

A Bytery payload is organized as:

```text
[magic][version][zmsk][present zones...]
```

For container v1, the canonical zone order is:

```text
[header][files][string-table][date-table][schema-table][data]
```

The `zmsk` byte decides which of those zones are present.
Absent zones are simply omitted.

In the following sections, each part of the payload is described in detail, starting with the **magic**.

## MAGIC

The **magic** is the first section of every Bytery payload.

It is a fixed 4-byte sequence used to identify the payload as a Bytery container.

The magic bytes are:

```text
BYT1 = 0x42 0x59 0x54 0x31
```

So, every valid Bytery payload must start with:

```text
0x42 0x59 0x54 0x31
```

This allows a decoder to quickly verify that the input is a Bytery payload before attempting to read the remaining sections.

If these 4 bytes do not match the expected magic, the payload must be considered invalid.

Important: the magic identifies the container format itself, not the compression mode.
For example, a raw Bytery payload starts with `BYT1`, while a GZIP-wrapped payload starts with the normal GZIP signature instead.

So, the magic is the fixed file signature that marks the beginning of every raw Bytery container.

## VERSION

The **version** is the second section of every raw Bytery payload.

It is stored immediately after the 4-byte magic and is encoded in exactly **1 byte**.

So, the beginning of a raw Bytery payload is always:

```text
[magic:4 bytes][version:1 byte]
```

In Bytery v1, the version byte is:

```text
0x01
```

The version byte tells the decoder which container version is being used.

This is important because the wire format may evolve over time, and future versions may introduce new rules, sections, or encodings.

If the version byte is not supported by the decoder, the payload must be considered unsupported.

So, the version is the fixed 1-byte value that immediately follows the magic and identifies which version of the Bytery container format is being used.

## ZMSK

The **ZMSK** is the third section of every raw Bytery payload.

It is stored immediately after the version byte and is encoded in exactly **1 byte** in container v1.

Its purpose is to declare which top-level zones are present in the payload.

So, the beginning of a raw Bytery v1 payload is always:

```text
[magic:4 bytes][version:1 byte][zmsk:1 byte]
```

### Bit layout

For the first `ZMSK` byte in v1:

* bit `0` = `header`
* bit `1` = `files`
* bit `2` = `string-table`
* bit `3` = `date-table`
* bit `4` = `schema-table`
* bit `5` = `data`
* bit `6` = reserved
* bit `7` = continuation flag (`has-next`)

So the defined v1 zone bits are:

```text
bit 0 = 0x01 = HEADERS
bit 1 = 0x02 = FILES
bit 2 = 0x04 = STRING TABLE
bit 3 = 0x08 = DATE TABLE
bit 4 = 0x10 = SCHEMA TABLE
bit 5 = 0x20 = DATA
```

### Continuation flag

Bit `7` is reserved as a continuation flag for future extension.

If that bit is set, another `ZMSK` byte would follow.
However, **container v1 currently uses only a single ZMSK byte**, so normal v1 payloads keep that bit clear.

### Example

If a payload contains:

* no header
* no files
* string table present
* no date table
* schema table present
* data present

then the `ZMSK` byte is:

```text
0x04 | 0x10 | 0x20 = 0x34
```

So the first bytes of the payload are:

```text
42 59 54 31 01 34
```

Meaning:

* `42 59 54 31` = `BYT1`
* `01` = version `1`
* `34` = string table + schema table + data

## HEADER

The **header** is an optional metadata zone of a raw Bytery payload.

Its purpose is to store **key-value metadata** that is not part of the main data section itself, but still belongs to the payload.

The header is present only when its bit is enabled in the `ZMSK`.

If the header zone is absent, it is simply omitted from the payload.

If the header zone is present, it is encoded as:

```text
[LUINT headerByteLength][LUINT pairCount][header entries...]
```

This means:

* the first value is the total byte length of the whole header body
* the second value is the number of header entries
* after that, the header entries are stored one after another

So, the header has only one wire form when present:

```text
[LUINT headerByteLength][LUINT pairCount][entry1][entry2][entry3]...
```

### Header entries

Each header entry is encoded as:

```text
[key:LSTR][typeCode:1 byte][value]
```

Meaning:

* `key` is stored as an `LSTR`
* `typeCode` is stored in **1 byte**
* `value` is encoded according to that type

The header key always uses `LSTR`, not `DSTR`.
In other words, header keys are always written literally and do not use the string table.

### Supported header value types

Header values support only primitive base types and primitive arrays.

The supported base type ids are:

* `1 = Integer`
* `2 = FLOAT4BYTES`
* `3 = FLOAT8BYTES`
* `4 = Boolean`
* `5 = Date`
* `6 = String`
* `7 = Bytes`

Array values use the same base type ids with `ArrayFlag = 0x80`:

* `129 = Integer[]`
* `130 = FLOAT4BYTES[]`
* `131 = FLOAT8BYTES[]`
* `132 = Boolean[]`
* `133 = Date[]`
* `134 = String[]`
* `135 = Bytes[]`

Important: header values do **not** support `Object` or `Object[]`.

### Header value encoding

The header value is encoded according to its `typeCode`.

Scalar values use the same primitive encodings already described earlier:

* `Integer` uses `LINT`
* `FLOAT4BYTES` uses the 4-byte float encoding
* `FLOAT8BYTES` uses the 8-byte float encoding
* `Boolean` uses `BOOL`
* `Date` uses the inline date encoding
* `String` uses `LSTR`
* `Bytes` uses `BARR`

Array values are encoded as:

```text
[LUINT countOrNull][element1][element2][element3]...
```

So, a header array may be:

* **NULL**
* empty
* or contain one or more primitive elements

Each element is then encoded using the same primitive encoding rules of its base type.

### Why `headerByteLength` exists

The `headerByteLength` tells the decoder exactly how many bytes belong to the header body.

This makes the section easier to validate and skip.

For example, a decoder may:

* read the header entries normally
* skip the header completely
* validate that the number of consumed bytes matches the declared header length

So, the header has both:

* a **total byte length**
* a **pair count**

The byte length tells how many bytes belong to the whole header body.
The pair count tells how many entries exist.

### Example: one header entry

Suppose the payload has this header:

```text
build = "v1"
```

This is encoded conceptually as:

```text
[headerByteLength][pairCount=1][key="build"][typeCode=String][value="v1"]
```

Or, more explicitly:

```text
[LUINT headerByteLength][LUINT 1][LSTR "build"][6][LSTR "v1"]
```

Meaning:

* `headerByteLength` is the total number of bytes occupied by the header body
* `1` means the header contains one entry
* `"build"` is the header key
* `6` means the value type is `String`
* `"v1"` is the header value

### Summary

So, the header is an optional metadata zone declared by `ZMSK`.

When present, its structure is:

```text
[LUINT headerByteLength][LUINT pairCount][entries...]
```

Each entry is:

```text
[key:LSTR][typeCode][value]
```

And it is used to store primitive metadata that belongs to the payload, but is not part of the main data section.

## FILES

The **files** zone is an optional raw-file zone of a Bytery payload.

Its purpose is to store file entries associated with the payload, without mixing them into the main data value.

The files zone is present only when its bit is enabled in the `ZMSK`.

If the files zone is absent, it is simply omitted from the payload.

If the files zone is present, it is encoded as:

```text
[LUINT filesByteLength][LUINT fileCount][file1][file2][file3]...
```

This means:

* the first value is the total byte length of the whole files body
* the second value is the number of files stored in the zone
* after that, the file entries are stored one after another

So, the files zone has only one wire form when present:

```text
[LUINT filesByteLength][LUINT fileCount][file1][file2][file3]...
```

Each file entry is encoded as:

```text
[fileName:LSTR][filePayload:BARR]
```

Meaning:

* `fileName` is stored literally as `LSTR`
* `filePayload` is stored as `BARR`
* files are stored in order

So:

* the first value is the total byte length of the files body
* the second value is the number of files stored in the zone
* each file has a literal file name
* each file has a raw byte payload

Important: file names are intentionally literal and do not use the string table.

### Why `filesByteLength` exists

The `filesByteLength` tells the decoder exactly how many bytes belong to the files body.

This makes the section easier to validate and skip.

For example, a decoder may:

* read the file entries normally
* skip the files zone completely
* validate that the number of consumed bytes matches the declared files length

So, the files zone has both:

* a **total byte length**
* a **file count**

The byte length tells how many bytes belong to the whole files body.
The file count tells how many file entries exist.

### Example

Suppose the payload contains one file:

```text
"readme.txt" = [0x48 0x69]
```

This is encoded conceptually as:

```text
[filesByteLength][fileCount=1][fileName="readme.txt"][filePayload=[0x48 0x69]]
```

Or, more explicitly:

```text
[LUINT filesByteLength][LUINT 1][LSTR "readme.txt"][BARR [0x48 0x69]]
```

Meaning:

* `filesByteLength` is the total number of bytes occupied by the files body
* there is **1** file entry
* `"readme.txt"` is the file name
* `[0x48 0x69]` is the raw byte payload

### Summary

So, the files zone is an optional zone declared by `ZMSK`.

When present, its structure is:

```text
[LUINT filesByteLength][LUINT fileCount][entries...]
```

Each entry is:

```text
[fileName:LSTR][filePayload:BARR]
```

And it is used to attach named raw binary files to the container while keeping the main payload data independent from them.

## STRING TABLE

The **string table** is the section that stores cached string values for the payload.

It appears in canonical zone order after `header` and `files`, but only if its bit is enabled in the `ZMSK`.
If the string table zone is absent, it is simply omitted from the payload.

Its purpose is to avoid writing the same string many times in the payload.  
Instead of repeating the full UTF-8 bytes every time, Bytery can store the string once in the string table and then refer to it using `DSTR` pointers.

The string table is encoded as:

```text
[LUINT stringCount][string1:LSTR][string2:LSTR][string3:LSTR]...
```

Meaning:

* the first value is the number of strings stored in the table
* each string-table entry is encoded as an `LSTR`
* strings are stored in order, and their position in that order is their **string table index**

So:

* the first string has index `0`
* the second string has index `1`
* the third string has index `2`
* and so on

## Why the string table exists

In many payloads, the same strings appear many times.

Common examples are:

* repeated field names
* repeated customer names
* repeated status values
* repeated country names
* repeated category labels
* repeated identifiers or tags

Without a string table, the same UTF-8 bytes would need to be written again and again.

With a string table, the string is written once as an `LSTR`, and later occurrences can be represented by a small `DSTR` pointer.

This can reduce both:

* payload size
* decoding overhead

## String table entry format

Each entry in the string table is an `LSTR`, so each stored string is encoded as:

```text
[LUINT byteLengthOrNull][UTF-8 bytes]
```

Important: in normal payloads, string-table entries are expected to be real strings.
A null string-table entry would not usually make sense.

So, conceptually, the string table is:

```text
[stringCount][literal string][literal string][literal string]...
```

## Relationship with `DSTR`

The string table is used by `DSTR`.

When a `DSTR` is encoded as a pointer, it does not store the UTF-8 bytes directly.
Instead, it stores an index into the string table.

For example:

* if string table index `0` contains `"John"`
* and a `DSTR` value is encoded as pointer `0`

then the decoder reads the `DSTR`, resolves index `0`, and loads `"John"` from the string table.

So, the string table is the target of all `DSTR` pointer-based string reuse.

## Example: string table with 3 strings

Suppose the string table contains:

```text
0 = "John"
1 = "active"
2 = "Brazil"
```

Conceptually, the section is:

```text
[3]["John"]["active"]["Brazil"]
```

Encoded as:

```text
[LUINT 3][LSTR "John"][LSTR "active"][LSTR "Brazil"]
```

Expanded by lengths:

```text
[3][4][0x4A][0x6F][0x68][0x6E][6][0x61][0x63][0x74][0x69][0x76][0x65][6][0x42][0x72][0x61][0x7A][0x69][0x6C]
```

Meaning:

* `[3]` = the table has **3 strings**
* `[4]["John"]` = string at index `0`
* `[6]["active"]` = string at index `1`
* `[6]["Brazil"]` = string at index `2`

So the decoder builds this table:

```text
index 0 -> "John"
index 1 -> "active"
index 2 -> "Brazil"
```

## Example: using the string table through `DSTR`

Suppose the string table is:

```text
0 = "John"
1 = "active"
2 = "Brazil"
```

Then these `DSTR` values may point to it:

* `[157]` = string table index `0`
* `[158]` = string table index `1`
* `[159]` = string table index `2`

Because for literal string-table pointers:

```text
stringTableIndex = currentByte - 157
```

So:

* `157 - 157 = 0`
* `158 - 157 = 1`
* `159 - 157 = 2`

This means a repeated string such as `"John"` can later be represented with just:

```text
[157]
```

instead of writing all UTF-8 bytes again.

## Field names also use the string table

The string table is not used only for data values.

Schema field names may also be encoded as `DSTR`, which means they can also point to the string table.

This is important because field names are often repeated across schemas, and storing them once can save many bytes.

So, the string table may contain both:

* repeated data strings
* repeated schema field names

## Optional string table zone

If the payload does not need any cached strings, the encoder may simply omit the string-table zone by clearing its bit in the `ZMSK`.

If an implementation chooses to emit the string-table zone anyway, it may still encode it as:

```text
[0]
```

Meaning:

* `0` = the table contains no strings

So, string-table absence is decided at the container level by `ZMSK`, not by a mandatory in-zone empty marker.

## Summary

So, the string table is the payload section that stores cached strings for later reuse.

Its structure is:

```text
[LUINT stringCount][string1:LSTR][string2:LSTR][string3:LSTR]...
```

Each string is stored literally as an `LSTR`, and its position defines its string-table index.

Later, `DSTR` values may point to those indexes instead of repeating the same UTF-8 text again.

This is one of the main reasons Bytery can reduce payload size so aggressively when the same strings appear many times.

## DATE TABLE

The **date table** is the section that stores cached date values for the payload.

It appears in canonical zone order after the string table, but only if its bit is enabled in the `ZMSK`.
If the date-table zone is absent, it is simply omitted from the payload.

Its purpose is to avoid writing the same date many times in the payload.  
Instead of repeating the full 8-byte UTC ticks value every time, Bytery can store the date once in the date table and then refer to it using `DDATE` pointers.

The date table is encoded as:

```text
[LUINT dateCount][date1:LDATE][date2:LDATE][date3:LDATE]...
```

Meaning:

* the first value is the number of dates stored in the table
* each date-table entry is encoded as an `LDATE`
* dates are stored in order, and their position in that order is their **date table index**

So:

* the first date has index `0`
* the second date has index `1`
* the third date has index `2`
* and so on

## Why the date table exists

In many payloads, the same date may appear many times.

Common examples are:

* the same creation timestamp repeated across related records
* the same event date repeated in multiple objects
* the same billing period date repeated in many entries
* the same base UTC date reused in nested structures

Without a date table, the same 8-byte ticks value would need to be written again and again.

With a date table, the date is written once as an `LDATE`, and later occurrences can be represented by a small `DDATE` pointer.

This can reduce both:

* payload size
* decoding overhead

## Date-table entry format

Each entry in the date table is an `LDATE`, so each stored date is encoded as:

```text
[8-byte UTC ticks, big-endian]
```

Important: a date-table entry is always a literal date value.
The date table itself does not store date pointers.

So, conceptually, the date table is:

```text
[dateCount][literal date][literal date][literal date]...
```

## Relationship with `DDATE`

The date table is used by `DDATE`.

When a `DDATE` is encoded as a pointer, it does not store the 8-byte UTC ticks directly.
Instead, it stores an index into the date table.

For example:

* if date table index `0` contains `2025-01-01T00:00:00Z`
* and a `DDATE` value is encoded as pointer `0`

then the decoder reads the `DDATE`, resolves index `0`, and loads that date from the date table.

So, the date table is the target of all `DDATE` pointer-based date reuse.

## Example: date table with 3 dates

Suppose the date table contains:

```text
0 = 2025-01-01T00:00:00Z
1 = 2025-01-02T00:00:00Z
2 = 2025-01-03T00:00:00Z
```

Conceptually, the section is:

```text
[3][date0][date1][date2]
```

Encoded as:

```text
[LUINT 3][LDATE date0][LDATE date1][LDATE date2]
```

Meaning:

* `[3]` = the table has **3 dates**
* the next 8 bytes = date at index `0`
* the next 8 bytes = date at index `1`
* the next 8 bytes = date at index `2`

So the decoder builds this table:

```text
index 0 -> 2025-01-01T00:00:00Z
index 1 -> 2025-01-02T00:00:00Z
index 2 -> 2025-01-03T00:00:00Z
```

## Example: using the date table through `DDATE`

Suppose the date table is:

```text
0 = 2025-01-01T00:00:00Z
1 = 2025-01-02T00:00:00Z
2 = 2025-01-03T00:00:00Z
```

Then these `DDATE` values may point to it:

* `[0x2C]` = date table index `0`
* `[0x2D]` = date table index `1`
* `[0x2E]` = date table index `2`

Because for literal date-table pointers:

```text
dateTableIndex = currentByte - 0x2C
```

So:

* `0x2C - 0x2C = 0`
* `0x2D - 0x2C = 1`
* `0x2E - 0x2C = 2`

This means a repeated date can later be represented with just one pointer byte, instead of writing the full 8-byte ticks value again.

## Optional date table zone

If the payload does not need any cached dates, the encoder may simply omit the date-table zone by clearing its bit in the `ZMSK`.

If an implementation chooses to emit the date-table zone anyway, it may still encode it as:

```text
[0]
```

Meaning:

* `0` = the table contains no dates

So, date-table absence is decided at the container level by `ZMSK`, not by a mandatory in-zone empty marker.

## Implementation strategy

The protocol defines **how** the date table is encoded and how `DDATE` pointers refer to it, but it does **not** require a specific strategy for deciding when a date should be written inline or stored in the date table.

That decision belongs to the implementation.

A good general recommendation is:

* write **unique dates inline**
* store **repeated dates** in the date table

However, depending on the implementation, it may not be possible to know whether a date will repeat later without doing a **second pass** over the data.

A full second pass may improve compression decisions, but it also costs extra processing.

A practical strategy is:

* write the **first occurrence** of a date inline
* if the same date appears a **second time**, add it to the date table and start using pointers for later occurrences

This avoids second-pass processing and back-tracking, while still capturing dates that are likely to repeat again.

So, the exact policy for populating the date table is implementation-defined, but the protocol fully defines how the table and its pointers must be encoded once that decision has been made.

## Summary

So, the date table is the payload section that stores cached dates for later reuse.

Its structure is:

```text
[LUINT dateCount][date1:LDATE][date2:LDATE][date3:LDATE]...
```

Each date is stored literally as an `LDATE`, and its position defines its date-table index.

Later, `DDATE` values may point to those indexes instead of repeating the same 8-byte UTC ticks value again.

This is one of the mechanisms that helps Bytery reduce payload size when the same dates appear many times.

## SCHEMA TABLE

The **schema table** is the section that stores all schemas used by the payload.

It appears in canonical zone order after the date table, but only if its bit is enabled in the `ZMSK`.
If the schema-table zone is absent, it is simply omitted from the payload.

Its purpose is to describe the structure of complex values, so the data section does not need to repeat field names and field types for every object.

The schema table is encoded as:

```text
[LUINT schemaCount][schema1][schema2][schema3]...
```

Meaning:

* the first value is the number of schemas stored in the table
* each schema-table entry starts with an `SMAT`
* the bytes that follow each `SMAT` depend on the schema type described by that `SMAT`
* schemas are stored in order, and their position in that order is their **schema table index**

So:

* the first schema has index `0`
* the second schema has index `1`
* the third schema has index `2`
* and so on

## Why the schema table exists

In JSON, objects must repeat their full structure again and again:

* every field name is written every time
* the decoder must rediscover the structure of each object from the text itself
* repeated object shapes cost many extra bytes

Bytery takes a different approach.

If many objects share the same shape, that shape is written once in the **schema table**, and the data section only needs to point to that schema.

This can reduce both:

* payload size
* decoding overhead
* structural repetition

## Schema-table entry format

Each schema-table entry starts with an `SMAT`.

So, conceptually, the schema table is:

```text
[schemaCount][SMAT entry][SMAT entry][SMAT entry]...
```

But each schema entry may have a different internal layout depending on its `SMAT`.

The possible schema entry forms are:

### NULL schema

```text
[SMAT_NULL]
```

### Array schema

```text
[SMAT_ARRAY_TYPE][objectSchemaPointer if elementType = Object]
```

### Map schema

```text
[SMAT_MAP_TYPE][objectSchemaPointer if valueType = Object]
```

### Map-of-arrays schema

```text
[SMAT_MAP_ARRAY_TYPE][objectSchemaPointer if elementType = Object]
```

### Object schema

```text
[SMAT_OBJECT_FIELD_COUNT][FIELDS]*
```

Where each field is encoded as:

```text
[fieldType][fieldName:DSTR][objectSchemaPointer if baseType = Object]
```

So, the schema table is not made of fixed-size rows.
Each entry has variable size, and `SMAT` is what tells the decoder how to interpret each one.

## Relationship with `SOBJ`

The schema table is used by `SOBJ`.

When an object value is stored in the data section, it may:

* use the **expected schema**
* use a **schema override**
* represent an object array using an expected schema

Whenever an object needs a schema, the decoder resolves it through the **schema table**.

So, the schema table is the place where all object, map, and array structures are defined before the data section is decoded.

## Object schemas inside the schema table

Object schemas are the most important entries in the schema table.

They describe:

* how many fields the object has
* the order of those fields
* the type of each field
* which nested schema to use when a field has base type `Object`

An object schema entry is encoded as:

```text
[SMAT fieldCount or SMAT+LUINT fieldCount][field1][field2][field3]...
```

Each field is encoded as:

```text
[fieldType][fieldName:DSTR][objectSchemaPointer if baseType = Object]
```

This means an object schema defines both:

* the **field order**
* the **field types**

This is important because objects in the data section are decoded according to this exact schema definition.

## Schema pointers inside the schema table

Some schema-table entries must point to another schema-table entry.

This happens when:

* an array schema has element type `Object`
* a map schema has value type `Object`
* a map-of-arrays schema has element type `Object`
* an object field has base type `Object`
* an object field has base type `Object[]`

In those cases, Bytery uses the **schema pointer family**, not `LUINT`.

The formats are:

```text
[252][indexU8]
[253][indexUInt16BE]
[254][indexUInt24BE]
```

Meaning:

* `252`: schema index stored in the next **1 byte**
* `253`: schema index stored in the next **2 bytes** as `UInt16BE`
* `254`: schema index stored in the next **3 bytes** as `UInt24BE`

So, the schema table may contain schemas that point to other schemas, forming a schema tree.

## Example: schema table with 2 schemas

Suppose the payload needs these two schemas:

```text
schema #0 = {
  id: Integer,
  name: String,
  age: Integer
}

schema #1 = {
  id: Integer,
  name: String,
  age: Integer,
  pet: Object using schema #0
}
```

Conceptually, the schema table is:

```text
[2][schema #0][schema #1]
```

Encoded as:

```text
[LUINT 2]

[schema #0]
[3]
[1][2]["i""d"]
[6][4]["n""a""m""e"]
[1][3]["a""g""e"]

[schema #1]
[4]
[1][2]["i""d"]
[6][4]["n""a""m""e"]
[1][3]["a""g""e"]
[8][3]["p""e""t"][252][0]
```

Meaning:

* `[2]` = the table has **2 schemas**
* schema index `0` describes a simple object with `id`, `name`, and `age`
* schema index `1` describes another object with `id`, `name`, `age`, and `pet`
* the `pet` field uses schema pointer `[252][0]`, which points to schema-table entry `0`

So the decoder builds this schema table first, and later the data section can refer to those schemas by index.

## Optional schema table zone

If the payload does not need any schemas, the encoder may simply omit the schema-table zone by clearing its bit in the `ZMSK`.

If an implementation chooses to emit the schema-table zone anyway, it may still encode it as:

```text
[0]
```

Meaning:

* `0` = the table contains no schemas

This is possible for payloads that contain only root primitive values or primitive arrays.

So, schema-table absence is decided at the container level by `ZMSK`, not by a mandatory in-zone empty marker.

## Implementation note

The protocol defines exactly how schema-table entries are encoded, but the strategy used to build the schema table belongs to the implementation.

For example, an implementation may:

* deduplicate equal schemas globally
* deduplicate them per session
* assign indexes as schemas are discovered
* build the schema table before writing data
* or discover schemas while traversing the object graph

These choices affect how the encoder is implemented, but not how the final schema table must be decoded.

What matters for the protocol is:

* the schema count
* the order of the schema entries
* the bytes of each entry
* the schema indexes used by pointers

## Summary

So, the schema table is the payload section that stores all schemas used by the payload.

Its structure is:

```text
[LUINT schemaCount][schema1][schema2][schema3]...
```

Each schema entry starts with an `SMAT`, and `SMAT` defines how the rest of that schema entry must be read.

The schema table is what allows the data section to remain compact, because repeated object structures are written once here and then reused by schema pointers instead of being redefined over and over again.

## DATA

The **data** zone is the final zone in the canonical order of a raw Bytery payload.

It stores the **actual payload value**.

All previous zones exist to support decoding:

- the **magic** identifies the container
- the **version** identifies the container format version
- the **zmsk** declares which zones are present
- the **header** stores optional metadata
- the **files** zone stores optional raw file entries
- the **string table** stores cached strings
- the **date table** stores cached dates
- the **schema table** stores object, map, and array schemas

The **data** zone is where the real value starts.

Important: the data zone is present only when its bit is enabled in the `ZMSK`.
If the data zone is absent, it is simply omitted from the payload.

## Root forms

A Bytery data section can start in **three different ways**:

- **root null**
- **root primitive or primitive array**
- **root schema-driven value**

So, the first byte of the data section determines how the root must be decoded.

### Root null

If the first byte is:

```text
[255]
```

then the root value is **NULL**.

This is the null root marker.

### Root primitive or primitive array

If the first byte is a primitive `typeCode`, then the root value is a primitive scalar or a primitive array.

The supported primitive root type codes are:

* `1 = Integer`
* `2 = FLOAT4BYTES`
* `3 = FLOAT8BYTES`
* `4 = Boolean`
* `5 = Date`
* `6 = String`
* `7 = Bytes`

Primitive array roots use the same base type ids with `ArrayFlag = 0x80`:

* `129 = Integer[]`
* `130 = FLOAT4BYTES[]`
* `131 = FLOAT8BYTES[]`
* `132 = Boolean[]`
* `133 = Date[]`
* `134 = String[]`
* `135 = Bytes[]`

So, a primitive root is encoded as:

```text
[rootTypeCode][value]
```

And a primitive-array root is encoded as:

```text
[rootTypeCodeWithArrayFlag][arrayPayload]
```

### Root schema-driven value

If the root is not null and not a primitive scalar or primitive array, then the root is encoded as a **schema-driven value**.

This is used for:

* object roots
* map roots
* array roots whose element type is `Object`

A schema-driven root is encoded as:

```text
[rootSchemaPointer][rootBody]
```

The root schema pointer uses the **schema pointer family**, not `LUINT`:

```text
[252][indexU8]
[253][indexUInt16BE]
[254][indexUInt24BE]
```

After the root schema pointer, the decoder loads the correct schema from the **schema table** and reads the root body using that schema.

## Primitive root values

When the root is a primitive scalar, the payload after the `typeCode` is encoded using the normal primitive rules of that type:

* `Integer` uses `LINT`
* `FLOAT4BYTES` uses the 4-byte float encoding
* `FLOAT8BYTES` uses the 8-byte float encoding
* `Boolean` uses `BOOL`
* `Date` uses `DDATE`
* `String` uses `DSTR`
* `Bytes` uses `BARR`

So, for example, this root value:

```text
42
```

is encoded as:

```text
[1][42]
```

Meaning:

* `[1]` = `Integer`
* `[42]` = `LINT(42)`

## Primitive array root values

When the root is a primitive array, the payload after the `typeCode` is encoded as:

```text
[LUINT countOrNull][element1][element2][element3]...
```

Each element is encoded using the normal primitive rules of its base type.

So, for example, this root value:

```text
[10, 20, 30]
```

is encoded conceptually as:

```text
[129][3][10][20][30]
```

Meaning:

* `[129]` = `Integer[]`
* `[3]` = array count `3`
* `[10][20][30]` = the three `LINT` values

Important: in the general array encoding, arrays may be encoded as `NULL` using `LUINT NULL`.
However, if the **root value itself** is null, Bytery uses the dedicated root null marker `[255]` instead.

## Schema-driven root values

When the root is schema-driven, the bytes after the root schema pointer are read according to the root schema kind.

That root schema may describe:

* an **object**
* a **map**
* an **array**

So, the full root structure is:

```text
[rootSchemaPointer][body defined by the resolved schema]
```

### Root object body

If the root schema is an object schema, the root body starts with the object-slot marker for **expected schema**:

```text
[0][fieldValue1][fieldValue2][fieldValue3]...
```

That leading `0` is `SOBJ_PRESENT_EXPECTED_SCHEMA`.

So, even for a root object, the decoder still sees the same object-slot family used elsewhere in the protocol.
The difference is that the root schema pointer has already selected the expected root schema, so the root object always begins with the expected-schema marker.

The field names are **not** written in the data section.

The decoder already knows:

* how many fields exist
* the order of those fields
* the type of each field

because that information was already defined in the **schema table**.

This is one of the main reasons Bytery saves so much space compared to JSON.

### Root map body

If the root schema is a map schema, the root body is encoded as:

```text
[LUINT pairCount][key:DSTR][value][key:DSTR][value]...
```

Meaning:

* first, the decoder reads the number of pairs
* each key is always encoded as `DSTR`
* each value is decoded according to the map schema value type

So, the schema defines the map value type, and the data section provides only:

* the number of entries
* the keys
* the values

### Root array body

If the root schema is an array schema, the root body is encoded as:

```text
[LUINT countOrNull][element1][element2][element3]...
```

Each element is decoded according to the array schema element type.

If the element type is primitive, each element uses the corresponding primitive encoding.

If the element type is `Object`, each element is encoded as an **object slot** using `SOBJ`.

## Object slots inside the data section

Object values inside the data section are not always written in exactly the same way.

Whenever a schema field has base type `Object`, the actual field value is encoded using `SOBJ`.

This allows the field value to be:

* `NULL`
* an object using the expected schema
* an object using a schema override
* a primitive scalar override
* a primitive array override
* an object array override

So, whenever the decoder reaches an object field in the data section, it does **not** immediately read an object body.
It first reads the `SOBJ` marker and lets that marker decide how the next bytes must be interpreted.

This makes object fields flexible, while still keeping the main payload compact.

## Example: schema-driven root object

Suppose the root schema pointer is:

```text
[252][4]
```

Meaning:

* the root uses schema-table entry `4`

And suppose schema `#4` is:

```text
{
  id: Integer,
  name: String,
  age: Integer
}
```

Then the root data section is:

```text
[252][4][0][idValue][nameValue][ageValue]
```

The `0` is the `SOBJ_PRESENT_EXPECTED_SCHEMA` marker for the root object.

The field names are not repeated in the data section.
Only the values are stored, in the exact field order defined by schema `#4`.

So, if the object is:

```text
{ id: 1, name: "John", age: 30 }
```

the data section conceptually looks like:

```text
[rootSchemaPointer][0][1]["John"][30]
```

Where the exact encoding of each field value depends on its type:

* `id` uses `LINT`
* `name` uses `DSTR`
* `age` uses `LINT`

## Why the data section is compact

The data section is compact because it does **not** need to repeat structural information that is already known from earlier sections.

For example:

* strings may be reused through the **string table**
* dates may be reused through the **date table**
* object structure is reused through the **schema table**
* primitive values use compact encodings such as `LINT`, `BOOL`, and `DSTR`

So the data section mostly contains:

* actual values
* indexes
* compact markers

instead of repeated textual structure.

## Summary

So, the data section stores the real payload value.

Its root form can be:

```text
[255]
```

or

```text
[rootTypeCode][value]
```

or

```text
[rootSchemaPointer][rootBody]
```

For object roots, `rootBody` begins with `SOBJ_PRESENT_EXPECTED_SCHEMA = [0]`.
For map and array roots, the body starts directly with the map or array payload defined by the resolved schema.

After that, the decoder uses the previously loaded tables and schemas to interpret the remaining bytes correctly.

This is the final section of the payload, and it is the point where all previous sections come together.

## FULL EXAMPLES

For clarity, the examples below use this simple encoding strategy:

- **no header**
- **no files**
- **no date table**
- **all non-null strings are stored in the string table**
- field names in schemas use `DSTR` pointers into the string table
- string values in data also use `DSTR` pointers into the string table

So, in these examples, the `ZMSK` is:

```text
0x04 | 0x10 | 0x20 = 0x34
```

Meaning:

* `STRING TABLE = present`
* `SCHEMA TABLE = present`
* `DATA = present`

This is only one possible encoder strategy, but it makes the full payload easier to understand.

---

### Example 1: simple client

Source object:

```text
client{
  id: 1,
  name: "John",
  age: 30
}
```

#### Step 1: MAGIC + VERSION + ZMSK

The payload always starts with:

```text
[magic][version][zmsk]
```

In this example:

* there is no header
* there are no files
* there is no date table
* the string table is present
* the schema table is present
* the data zone is present

So:

```text
zmsk = 0x04 | 0x10 | 0x20 = 0x34
```

The first bytes are:

```text
[0x42][0x59][0x54][0x31][0x01][0x34]
```

Meaning:

* `0x42 0x59 0x54 0x31` = `BYT1`
* `0x01` = version 1
* `0x34` = string table + schema table + data

So far, the byte vector is:

```text
42 59 54 31 01 34
```

#### Step 2: STRING TABLE

We will store these strings in the string table:

```text
index 0 = "id"
index 1 = "name"
index 2 = "age"
index 3 = "John"
```

So the string table is:

```text
[4]["id"]["name"]["age"]["John"]
```

Encoded as:

```text
[4]
[2][0x69][0x64]
[4][0x6E][0x61][0x6D][0x65]
[3][0x61][0x67][0x65]
[4][0x4A][0x6F][0x68][0x6E]
```

So now the byte vector is:

```text
42 59 54 31 01 34
04 02 69 64 04 6E 61 6D 65 03 61 67 65 04 4A 6F 68 6E
```

#### Step 3: SCHEMA TABLE

We only need one schema:

```text
schema #0 = {
  id: Integer,
  name: String,
  age: Integer
}
```

The schema table starts with the schema count:

```text
[1]
```

Now we encode schema `#0`.

This is an object schema with 3 fields, so it starts with:

```text
[3]
```

Then each field is:

```text
[fieldType][fieldName:DSTR]
```

Field 1: `id: Integer`

* `Integer = 1`
* `"id"` is string-table index `0`
* `DSTR pointer = 157 + 0 = 157 = 0x9D`

So:

```text
[1][0x9D]
```

Field 2: `name: String`

* `String = 6`
* `"name"` is index `1`
* `DSTR pointer = 157 + 1 = 158 = 0x9E`

So:

```text
[6][0x9E]
```

Field 3: `age: Integer`

* `Integer = 1`
* `"age"` is index `2`
* `DSTR pointer = 157 + 2 = 159 = 0x9F`

So:

```text
[1][0x9F]
```

So schema `#0` is:

```text
[3][1][0x9D][6][0x9E][1][0x9F]
```

And the whole schema table is:

```text
[1][3][1][0x9D][6][0x9E][1][0x9F]
```

Now the byte vector is:

```text
42 59 54 31 01 34
04 02 69 64 04 6E 61 6D 65 03 61 67 65 04 4A 6F 68 6E
01 03 01 9D 06 9E 01 9F
```

#### Step 4: DATA

The root is an object, so the data starts with a root schema pointer.

The root uses schema `#0`, so the schema pointer is:

```text
[252][0]
```

In hex:

```text
FC 00
```

Because the root schema is an object schema, the root body begins with the expected-schema object marker:

```text
[0]
```

Now we write the field values in schema order:

```text
id, name, age
```

So the values are:

* `id = 1` → `LINT(1)` = `[1]`
* `name = "John"` → string-table index `3` → `DSTR pointer = 157 + 3 = 160 = 0xA0`
* `age = 30` → `LINT(30)` = `[30] = [0x1E]`

So the data zone is:

```text
[252][0][0][1][0xA0][30]
```

In hex:

```text
FC 00 00 01 A0 1E
```

#### Final payload

So the full payload is:

```text
42 59 54 31 01 34
04 02 69 64 04 6E 61 6D 65 03 61 67 65 04 4A 6F 68 6E
01 03 01 9D 06 9E 01 9F
FC 00 00 01 A0 1E
```

Or in one line:

```text
42 59 54 31 01 34 04 02 69 64 04 6E 61 6D 65 03 61 67 65 04 4A 6F 68 6E 01 03 01 9D 06 9E 01 9F FC 00 00 01 A0 1E
```

---

### Example 2: nested client with pet and integer array

Source object:

```text
client{
  id: 1,
  name: "John",
  age: 30,
  pet: {
    id: 3,
    name: "Whiskey",
    age: 3
  },
  nums: [1,2,3]
}
```

#### Step 1: MAGIC + VERSION + ZMSK

Same strategy as before:

* no header
* no files
* no date table
* string table present
* schema table present
* data present

So the beginning is:

```text
42 59 54 31 01 34
```

#### Step 2: STRING TABLE

We will store these strings:

```text
index 0 = "id"
index 1 = "name"
index 2 = "age"
index 3 = "pet"
index 4 = "nums"
index 5 = "John"
index 6 = "Whiskey"
```

So the string table is:

```text
[7]["id"]["name"]["age"]["pet"]["nums"]["John"]["Whiskey"]
```

Encoded as:

```text
[7]
[2][0x69][0x64]
[4][0x6E][0x61][0x6D][0x65]
[3][0x61][0x67][0x65]
[3][0x70][0x65][0x74]
[4][0x6E][0x75][0x6D][0x73]
[4][0x4A][0x6F][0x68][0x6E]
[7][0x57][0x68][0x69][0x73][0x6B][0x65][0x79]
```

So now the byte vector is:

```text
42 59 54 31 01 34
07 02 69 64 04 6E 61 6D 65 03 61 67 65 03 70 65 74 04 6E 75 6D 73 04 4A 6F 68 6E 07 57 68 69 73 6B 65 79
```

#### Step 3: SCHEMA TABLE

We need two schemas:

```text
schema #0 = {
  id: Integer,
  name: String,
  age: Integer
}

schema #1 = {
  id: Integer,
  name: String,
  age: Integer,
  pet: Object using schema #0,
  nums: Integer[]
}
```

The schema table starts with:

```text
[2]
```

because there are 2 schemas.

##### Schema #0

This is an object schema with 3 fields:

```text
[3]
```

Fields:

* `id: Integer` → `[1][0x9D]`
* `name: String` → `[6][0x9E]`
* `age: Integer` → `[1][0x9F]`

So schema `#0` is:

```text
[3][1][0x9D][6][0x9E][1][0x9F]
```

##### Schema #1

This is an object schema with 5 fields:

```text
[5]
```

Fields:

* `id: Integer` → `[1][0x9D]`

* `name: String` → `[6][0x9E]`

* `age: Integer` → `[1][0x9F]`

* `pet: Object using schema #0`

  * `Object = 8`
  * `"pet"` = index `3` → `0xA0`
  * schema pointer to `#0` = `[252][0]` = `[0xFC][0x00]`
  * full field = `[8][0xA0][0xFC][0x00]`

* `nums: Integer[]`

  * `Integer[] = 129 = 0x81`
  * `"nums"` = index `4` → `0xA1`
  * full field = `[129][0xA1]`

So schema `#1` is:

```text
[5][1][0x9D][6][0x9E][1][0x9F][8][0xA0][0xFC][0x00][129][0xA1]
```

So the whole schema table is:

```text
[2]
[3][1][0x9D][6][0x9E][1][0x9F]
[5][1][0x9D][6][0x9E][1][0x9F][8][0xA0][0xFC][0x00][129][0xA1]
```

Now the byte vector is:

```text
42 59 54 31 01 34
07 02 69 64 04 6E 61 6D 65 03 61 67 65 03 70 65 74 04 6E 75 6D 73 04 4A 6F 68 6E 07 57 68 69 73 6B 65 79
02
03 01 9D 06 9E 01 9F
05 01 9D 06 9E 01 9F 08 A0 FC 00 81 A1
```

#### Step 4: DATA

The root is the full `client` object, so the root uses schema `#1`.

The root schema pointer is:

```text
[252][1]
```

In hex:

```text
FC 01
```

Because the root schema is an object schema, the root body begins with the expected-schema object marker:

```text
[0]
```

Now we write the field values in schema `#1` order:

```text
id, name, age, pet, nums
```

##### `id = 1`

```text
[1]
```

##### `name = "John"`

`"John"` is string-table index `5`.

So the `DSTR` pointer is:

```text
157 + 5 = 162 = 0xA2
```

So:

```text
[A2]
```

##### `age = 30`

```text
[30] = [0x1E]
```

##### `pet = { id: 3, name: "Whiskey", age: 3 }`

The field base type is `Object`, so the value starts with an `SOBJ`.

The pet object uses the **expected schema** `#0`, so the `SOBJ` marker is:

```text
[0]
```

Then we write the pet body using schema `#0` field order:

```text
id, name, age
```

So:

* `id = 3` → `[3]`
* `"Whiskey"` = string-table index `6` → `157 + 6 = 163 = 0xA3`
* `age = 3` → `[3]`

So pet is:

```text
[0][3][0xA3][3]
```

##### `nums = [1,2,3]`

This is an `Integer[]`, so it is encoded as:

```text
[count][item1][item2][item3]
```

Count is `3`, so:

```text
[3][1][2][3]
```

##### Full data zone

So the data zone is:

```text
[0xFC][0x01]
[0]
[1]
[0xA2]
[0x1E]
[0][3][0xA3][3]
[3][1][2][3]
```

In one line:

```text
FC 01 00 01 A2 1E 00 03 A3 03 03 01 02 03
```

#### Final payload

So the full payload is:

```text
42 59 54 31 01 34
07 02 69 64 04 6E 61 6D 65 03 61 67 65 03 70 65 74 04 6E 75 6D 73 04 4A 6F 68 6E 07 57 68 69 73 6B 65 79
02
03 01 9D 06 9E 01 9F
05 01 9D 06 9E 01 9F 08 A0 FC 00 81 A1
FC 01 00 01 A2 1E 00 03 A3 03 03 01 02 03
```

Or in one line:

```text
42 59 54 31 01 34 07 02 69 64 04 6E 61 6D 65 03 61 67 65 03 70 65 74 04 6E 75 6D 73 04 4A 6F 68 6E 07 57 68 69 73 6B 65 79 02 03 01 9D 06 9E 01 9F 05 01 9D 06 9E 01 9F 08 A0 FC 00 81 A1 FC 01 00 01 A2 1E 00 03 A3 03 03 01 02 03
```

See how compact the schema definitions are: some schemas can be represented with just **1 byte**, and even a nested structure such as the `client` tree can still be described with very few bytes.

---

## MINIMAL ROOT EXAMPLES

These are small complete payloads that show how Bytery can encode primitive roots, primitive-array roots, and null roots.

In all examples below:

* no header
* no files
* no string table
* no date table
* no schema table
* only the data zone is present

So the `ZMSK` is:

```text
0x20
```

Meaning:

* `DATA = present`

### Parse primitive `3`

```text
42 59 54 31 01 20 01 03
```

Meaning:

* `42 59 54 31` = magic
* `01` = version
* `20` = only data zone is present
* `01` = root type is `Integer`
* `03` = `LINT(3)`

So the root value is:

```text
3
```

### Parse primitive `"John"`

```text
42 59 54 31 01 20 06 04 4A 6F 68 6E
```

Meaning:

* `42 59 54 31` = magic
* `01` = version
* `20` = only data zone is present
* `06` = root type is `String`
* `04 4A 6F 68 6E` = `DSTR` literal `"John"`

So the root value is:

```text
"John"
```

### Parse primitive array `[1,2,3]`

```text
42 59 54 31 01 20 81 03 01 02 03
```

Meaning:

* `42 59 54 31` = magic
* `01` = version
* `20` = only data zone is present
* `81` = root type is `Integer[]`
* `03` = array count `3`
* `01 02 03` = the three integer values

So the root value is:

```text
[1,2,3]
```

### Parse null

```text
42 59 54 31 01 20 FF
```

Meaning:

* `42 59 54 31` = magic
* `01` = version
* `20` = only data zone is present
* `FF` in the data zone = root null

So the root value is:

```text
NULL
```

---

## REVERSE EXAMPLE

Let us now decode a complete Bytery payload step by step, starting from the raw bytes and reconstructing the original structure.

We will use this payload:

```text
42 59 54 31 01 35 1D 02 06 61 75 74 68 6F 72 06
06 62 79 74 65 72 79 04 77 68 65 6E 05 08 DE 89
90 C9 C6 DC C1 09 08 4E 65 77 20 59 6F 72 6B 04
4A 6F 68 6E 04 54 6F 62 79 03 61 67 65 02 69 64
04 6E 61 6D 65 04 63 69 74 79 04 6E 75 6D 73 03
70 65 74 02 03 01 A0 01 A1 06 A2 05 06 A3 01 A1
06 A2 81 A4 08 A5 FC 00 FC 01 00 9D 01 9E 03 01
02 03 00 0B 03 9F
```

### Step 1: MAGIC + VERSION + ZMSK

The first bytes are:

```text
42 59 54 31 01 35
```

Breaking that down:

```text
42 59 54 31 = "BYT1"
01          = version 1
35          = zmsk
```

So this is a valid raw Bytery v1 payload.

Now decode `ZMSK = 0x35`.

In binary:

```text
0x35 = 0b00110101
```

Using the v1 bit mapping:

* bit `0` = `HEADERS`
* bit `1` = `FILES`
* bit `2` = `STRING TABLE`
* bit `3` = `DATE TABLE`
* bit `4` = `SCHEMA TABLE`
* bit `5` = `DATA`

So `0x35` means:

* `HEADERS = present`
* `FILES = absent`
* `STRING TABLE = present`
* `DATE TABLE = absent`
* `SCHEMA TABLE = present`
* `DATA = present`

So the container layout is:

```text
[magic][version][zmsk][header][string-table][schema-table][data]
```

---

### Step 2: HEADER

The next bytes start the header zone:

```text
1D 02 ...
```

This means:

```text
1D = headerByteLength = 29
02 = pairCount = 2
```

So the header body occupies 29 bytes, and the header contains 2 entries.

#### Header entry 1

Next bytes:

```text
06 61 75 74 68 6F 72 06 06 62 79 74 65 72 79
```

Decode:

* `06` = LSTR length `6`
* `61 75 74 68 6F 72` = `"author"`
* `06` = typeCode `String`
* `06 62 79 74 65 72 79` = LSTR `"bytery"`

So:

```text
author = "bytery"
```

#### Header entry 2

Next bytes:

```text
04 77 68 65 6E 05 08 DE 89 90 C9 C6 DC C1
```

Decode:

* `04` = LSTR length `4`
* `77 68 65 6E` = `"when"`
* `05` = typeCode `Date`
* next 8 bytes = literal date payload

The 8-byte date value is:

```text
08 DE 89 90 C9 C6 DC C1
```

Which corresponds to:

```text
2026-03-24T10:33:28.6738113Z
```

So:

```text
when = 2026-03-24T10:33:28.6738113Z
```

So the decoded header is:

```text
{
  author: "bytery",
  when: 2026-03-24T10:33:28.6738113Z
}
```

---

### Step 3: FILES

The `FILES` bit is not set in `ZMSK`, so there is no files zone in this payload.

---

### Step 4: STRING TABLE

The next byte is:

```text
09
```

So the string table count is:

```text
9
```

Now decode the 9 string entries.

#### String 0

```text
08 4E 65 77 20 59 6F 72 6B
```

* `08` = length 8
* bytes = `"New York"`

So:

```text
index 0 = "New York"
```

#### String 1

```text
04 4A 6F 68 6E
```

So:

```text
index 1 = "John"
```

#### String 2

```text
04 54 6F 62 79
```

So:

```text
index 2 = "Toby"
```

#### String 3

```text
03 61 67 65
```

So:

```text
index 3 = "age"
```

#### String 4

```text
02 69 64
```

So:

```text
index 4 = "id"
```

#### String 5

```text
04 6E 61 6D 65
```

So:

```text
index 5 = "name"
```

#### String 6

```text
04 63 69 74 79
```

So:

```text
index 6 = "city"
```

#### String 7

```text
04 6E 75 6D 73
```

So:

```text
index 7 = "nums"
```

#### String 8

```text
03 70 65 74
```

So:

```text
index 8 = "pet"
```

So the full string table is:

```text
0 = "New York"
1 = "John"
2 = "Toby"
3 = "age"
4 = "id"
5 = "name"
6 = "city"
7 = "nums"
8 = "pet"
```

---

### Step 5: DATE TABLE

The `DATE TABLE` bit is not set in `ZMSK`, so there is no date table in this payload.

---

### Step 6: SCHEMA TABLE

The next byte is:

```text
02
```

So the schema count is:

```text
2
```

So this payload contains:

```text
schema #0
schema #1
```

#### Schema #0

The first schema starts with:

```text
03
```

Since `03` is in the object-schema literal field-count range, this means:

```text
schema #0 = object with 3 fields
```

Now read the fields.

##### Field 1

```text
01 A0
```

* `01` = `Integer`
* `A0 = 160`
* DSTR pointer index = `160 - 157 = 3`
* string table index `3` = `"age"`

So:

```text
age: Integer
```

##### Field 2

```text
01 A1
```

* `01` = `Integer`
* `A1 = 161`
* pointer index = `161 - 157 = 4`
* string table index `4` = `"id"`

So:

```text
id: Integer
```

##### Field 3

```text
06 A2
```

* `06` = `String`
* `A2 = 162`
* pointer index = `162 - 157 = 5`
* string table index `5` = `"name"`

So:

```text
name: String
```

Therefore:

```text
schema #0 = {
  age: Integer,
  id: Integer,
  name: String
}
```

#### Schema #1

The second schema starts with:

```text
05
```

So:

```text
schema #1 = object with 5 fields
```

Now decode its fields.

##### Field 1

```text
06 A3
```

* `06` = `String`
* `A3 = 163`
* pointer index = `163 - 157 = 6`
* string table index `6` = `"city"`

So:

```text
city: String
```

##### Field 2

```text
01 A1
```

So:

```text
id: Integer
```

##### Field 3

```text
06 A2
```

So:

```text
name: String
```

##### Field 4

```text
81 A4
```

* `81` = `Integer[]`
* `A4 = 164`
* pointer index = `164 - 157 = 7`
* string table index `7` = `"nums"`

So:

```text
nums: Integer[]
```

##### Field 5

```text
08 A5 FC 00
```

* `08` = `Object`
* `A5 = 165`
* pointer index = `165 - 157 = 8`
* string table index `8` = `"pet"`
* `FC 00` = schema pointer to schema `0`

So:

```text
pet: Object using schema #0
```

Therefore:

```text
schema #1 = {
  city: String,
  id: Integer,
  name: String,
  nums: Integer[],
  pet: Object using schema #0
}
```

---

### Step 7: DATA

The remaining bytes are:

```text
FC 01 00 9D 01 9E 03 01 02 03 00 0B 03 9F
```

#### Root schema pointer

The data starts with:

```text
FC 01
```

This is a schema pointer to:

```text
schema #1
```

So the root value uses `schema #1`.

#### Root SOBJ marker

Next byte:

```text
00
```

This is:

```text
SOBJ_PRESENT_EXPECTED_SCHEMA
```

Meaning the root object body follows using schema `#1` exactly as expected.

So now we read the field values in schema `#1` order:

```text
city, id, name, nums, pet
```

#### Field `city`

Next byte:

```text
9D
```

`9D = 157`, so the DSTR pointer index is:

```text
157 - 157 = 0
```

String table index `0` = `"New York"`

So:

```text
city = "New York"
```

#### Field `id`

Next byte:

```text
01
```

So:

```text
id = 1
```

#### Field `name`

Next byte:

```text
9E
```

`9E = 158`, so:

```text
index = 158 - 157 = 1
```

String table index `1` = `"John"`

So:

```text
name = "John"
```

#### Field `nums`

Next bytes:

```text
03 01 02 03
```

Decode:

* `03` = array count `3`
* values = `1, 2, 3`

So:

```text
nums = [1,2,3]
```

#### Field `pet`

Next byte:

```text
00
```

This is again:

```text
SOBJ_PRESENT_EXPECTED_SCHEMA
```

So `pet` uses the expected schema `#0`.

Now read the body of schema `#0`, in field order:

```text
age, id, name
```

Remaining bytes:

```text
0B 03 9F
```

Decode:

* `0B` = `11`
* `03` = `3`
* `9F = 159` → pointer index `2` → string table index `2` = `"Toby"`

So:

```text
pet = {
  age: 11,
  id: 3,
  name: "Toby"
}
```

---

### Final reconstructed value

So the full decoded payload is:

```text
{
  city: "New York",
  id: 1,
  name: "John",
  nums: [1,2,3],
  pet: {
    age: 11,
    id: 3,
    name: "Toby"
  }
}
```

Or in JSON form:

```json
{"city":"New York","id":1,"name":"John","nums":[1,2,3],"pet":{"age":11,"id":3,"name":"Toby"}}
```

And the decoded header is:

```text
{
  author: "bytery",
  when: 2026-03-24T10:33:28.6738113Z
}
```

## GZIP disclaimer

**GZIP is not part of the Bytery protocol itself.**  
It is an **optional outer compression layer**, and whether it is used or not is entirely an **implementation decision**.

In other words:

- the Bytery protocol defines the raw binary container
- GZIP, when used, is applied **outside** that container
- a raw Bytery payload starts with `BYT1`
- a GZIP-wrapped Bytery payload starts with the normal GZIP signature instead

So, GZIP should be understood as a transport or storage optimization layer, not as a native internal section of the protocol.

In practice, however, GZIP can still be very useful.

In the benchmark scenarios shown earlier in this document, applying GZIP on top of the final Bytery payload produced an additional reduction of **more than 50%** in the final binary size, while adding roughly **20% to 30% more processing cost**, depending on the payload and test scenario.

This trade-off can still be very attractive, because the same benchmarks show that Bytery can be many times faster than plain JSON, and can also remain faster than JSON combined with GZIP.

So, although GZIP is not part of the protocol, it can be a very good implementation choice when reducing final payload size is more important than the extra compression and decompression cost.

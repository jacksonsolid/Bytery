const FILE_MAGIC_B0 = 0x42;
const FILE_MAGIC_B1 = 0x59;
const FILE_MAGIC_B2 = 0x54;
const FILE_MAGIC_B3 = 0x31;
const FILE_VERSION_V1 = 0x01;
const ZMSK_ZONE_BITS_MASK = 0x7f;
const ZMSK_HAS_NEXT = 0x80;
const ZMSK_HEADERS = 0x01;
const ZMSK_FILES = 0x02;
const ZMSK_STRING_TABLE = 0x04;
const ZMSK_DATE_TABLE = 0x08;
const ZMSK_SCHEMA_TABLE = 0x10;
const ZMSK_DATA = 0x20;
const ZMSK_V1_DEFINED_ZONES_MASK = 0x3f;
const LUINT_B0_MAX = 246;
const LUINT_B8 = 247;
const LUINT_B8_BASE_VALUE = LUINT_B0_MAX + 1;
const LUINT_B8_MAX = LUINT_B8_BASE_VALUE + 255;
const LUINT_16 = 248;
const LUINT_24 = 249;
const LUINT_32 = 250;
const LUINT_40 = 251;
const LUINT_48 = 252;
const LUINT_56 = 253;
const LUINT_64 = 254;
const LUINT_NULL = 255;
const LINT_POS_LITERAL_MAX_VALUE = 219;
const LINT_NEG_LITERAL_FIRST_TAG = 220;
const LINT_NEG_LITERAL_LAST_TAG = 238;
const LINT_NEG_LITERAL_COUNT = LINT_NEG_LITERAL_LAST_TAG - LINT_NEG_LITERAL_FIRST_TAG + 1;
const LINT_POS_PLUS_U8_BASE_VALUE = 220;
const LINT_POS_PLUS_U8_MAX_VALUE = LINT_POS_PLUS_U8_BASE_VALUE + 255;
const LINT_POS_PLUS_U8_TAG = 239;
const LINT_POS_U16_TAG = 240;
const LINT_POS_U24_TAG = 241;
const LINT_POS_U32_TAG = 242;
const LINT_POS_U40_TAG = 243;
const LINT_POS_U48_TAG = 244;
const LINT_POS_U56_TAG = 245;
const LINT_POS_U64_TAG = 246;
const LINT_NEG_PLUS_U8_BASE_MAG = LINT_NEG_LITERAL_COUNT + 1;
const LINT_NEG_PLUS_U8_MAX_MAG = LINT_NEG_PLUS_U8_BASE_MAG + 255;
const LINT_NEG_PLUS_U8_TAG = 247;
const LINT_NEG_U16_TAG = 248;
const LINT_NEG_U24_TAG = 249;
const LINT_NEG_U32_TAG = 250;
const LINT_NEG_U40_TAG = 251;
const LINT_NEG_U48_TAG = 252;
const LINT_NEG_U56_TAG = 253;
const LINT_NEG_U64_TAG = 254;
const LINT_NULL_TAG = 255;
const DSTR_LEN_LITERAL_MAX = 156;
const DSTR_PTR_LITERAL_BASE_TAG = 157;
const DSTR_PTR_LITERAL_COUNT = 90;
const DSTR_PTR_LITERAL_MAX_TAG = 246;
const DSTR_LEN_U8_TAG = 247;
const DSTR_LEN_U16_TAG = 248;
const DSTR_LEN_U24_TAG = 249;
const DSTR_LEN_U32_TAG = 250;
const DSTR_LEN_U8_BASE = DSTR_LEN_LITERAL_MAX + 1;
const DSTR_PTR_U8_TAG = 251;
const DSTR_PTR_U16_TAG = 252;
const DSTR_PTR_U24_TAG = 253;
const DSTR_PTR_U32_TAG = 254;
const DSTR_NULL_TAG = 255;
const BOOL_FALSE = 0;
const BOOL_TRUE = 1;
const BOOL_NULL = 2;
const SOBJ_PRESENT_EXPECTED_SCHEMA = 0;
const SOBJ_SCHEMA_PTR_1B_TAG = 252;
const SOBJ_SCHEMA_PTR_2B_TAG = 253;
const SOBJ_SCHEMA_PTR_3B_TAG = 254;
const SOBJ_NULL_TAG = 255;
const SMAT_NULL_TAG = 255;
const SMAT_OBJ_LITERAL_MAX_FIELDS = 229;
const SMAT_OBJ_LUINT_TAG = 230;
const SMAT_MAP_BASE_TAG = 231;
const SMAT_MAP_MAX_TAG = 238;
const SMAT_MAP_ARR_BASE_TAG = 239;
const SMAT_MAP_ARR_MAX_TAG = 246;
const SMAT_ARR_BASE_TAG = 247;
const SMAT_ARR_MAX_TAG = 254;
const LDATE_TICKS_B0_MAX = 0x2b;
const DDATE_PTR_LITERAL_BASE_TAG = 0x2c;
const DDATE_PTR_LITERAL_MAX_TAG = 0xfa;
const DDATE_PTR_LITERAL_COUNT = DDATE_PTR_LITERAL_MAX_TAG - DDATE_PTR_LITERAL_BASE_TAG + 1;
const DDATE_PTR_U8_TAG = 0xfb;
const DDATE_PTR_U16_TAG = 0xfc;
const DDATE_PTR_U24_TAG = 0xfd;
const DDATE_PTR_U32_TAG = 0xfe;
const DDATE_PTR_U8_BASE_INDEX = DDATE_PTR_LITERAL_COUNT;
const DDATE_PTR_U8_MAX_INDEX = DDATE_PTR_U8_BASE_INDEX + 255;
const DDATE_NULL_TAG = 0xff;
const FLOAT_NULL_SENTINEL_B0 = 0xff;
const FLOAT_NULL_SENTINEL_B1 = 0xff;
const DOTNET_UNIX_EPOCH_TICKS = 621355968000000000n;
const TICKS_PER_MILLISECOND = 10000n;
const JS_DATE_MIN_MS = -8640000000000000n;
const JS_DATE_MAX_MS = 8640000000000000n;
const JS_MAX_SAFE_BIGINT = BigInt(Number.MAX_SAFE_INTEGER);
const JS_MIN_SAFE_BIGINT = BigInt(Number.MIN_SAFE_INTEGER);
const UTF8_DECODER = new TextDecoder("utf-8", { fatal: true });
const UTF8_ENCODER = new TextEncoder();
function toUint8Array(source) {
    if (source instanceof Uint8Array)
        return source;
    if (source instanceof ArrayBuffer)
        return new Uint8Array(source);
    if (ArrayBuffer.isView(source)) {
        return new Uint8Array(source.buffer, source.byteOffset, source.byteLength);
    }
    const maybeArrayLike = source;
    if (typeof maybeArrayLike.length === "number") {
        return Uint8Array.from(maybeArrayLike);
    }
    return Uint8Array.from(source);
}
function toBytesInput(source) {
    if (source instanceof Uint8Array)
        return source;
    if (source instanceof ArrayBuffer)
        return new Uint8Array(source);
    return new Uint8Array(source.buffer, source.byteOffset, source.byteLength);
}
function isGZip(data) {
    return data.length >= 2 && data[0] === 0x1f && data[1] === 0x8b;
}
function isBytery(data) {
    return (data.length >= 5 &&
        data[0] === FILE_MAGIC_B0 &&
        data[1] === FILE_MAGIC_B1 &&
        data[2] === FILE_MAGIC_B2 &&
        data[3] === FILE_MAGIC_B3);
}
function isJson(data) {
    if (data.length === 0)
        return false;
    for (let i = 0; i < data.length; i++) {
        const b = data[i];
        switch (b) {
            case 9: // \t
            case 10: // \n
            case 13: // \r
            case 32: // space
                continue;
            case 0x7b: // {
            case 0x5b: // [
            case 0x22: // "
            case 0x2d: // -
            case 0x74: // t
            case 0x66: // f
            case 0x6e: // n
                return true;
            default:
                if (b >= 0x30 && b <= 0x39) {
                    return true;
                }
                return false;
        }
    }
    return false;
}
async function gunzip(data) {
    const DecompressionStreamCtor = globalThis.DecompressionStream;
    if (typeof DecompressionStreamCtor !== "function") {
        throw new Error("This runtime does not provide native GZIP decompression (DecompressionStream).");
    }
    // TypeScript can reject Uint8Array<ArrayBufferLike> as BlobPart.
    // Rebuild it as a fresh Uint8Array backed by a plain ArrayBuffer.
    const blobBytes = new Uint8Array(data);
    const stream = new Blob([blobBytes])
        .stream()
        .pipeThrough(new DecompressionStreamCtor("gzip"));
    const buffer = await new Response(stream).arrayBuffer();
    return new Uint8Array(buffer);
}
function isArrayType(typeCode) {
    return (typeCode & 128 /* FieldType.ArrayFlag */) === 128 /* FieldType.ArrayFlag */;
}
function baseType(typeCode) {
    return typeCode & ~128 /* FieldType.ArrayFlag */;
}
function losslessIntegerFromBigInt(value) {
    if (value >= JS_MIN_SAFE_BIGINT && value <= JS_MAX_SAFE_BIGINT) {
        return Number(value);
    }
    return value;
}
function ticksToJsDate(ticks) {
    const msSinceUnixEpoch = (ticks - DOTNET_UNIX_EPOCH_TICKS) / TICKS_PER_MILLISECOND;
    if (msSinceUnixEpoch < JS_DATE_MIN_MS || msSinceUnixEpoch > JS_DATE_MAX_MS) {
        throw new Error(`Bytery Date is outside JavaScript Date range: ticks=${ticks.toString()}`);
    }
    return new Date(Number(msSinceUnixEpoch));
}
function jsDateToTicks(value) {
    return BigInt(value.getTime()) * TICKS_PER_MILLISECOND + DOTNET_UNIX_EPOCH_TICKS;
}
function isByteLike(value) {
    return value instanceof ArrayBuffer || ArrayBuffer.isView(value);
}
function isPlainObject(value) {
    if (value === null || typeof value !== "object")
        return false;
    if (Array.isArray(value))
        return false;
    if (value instanceof Date)
        return false;
    if (value instanceof Map)
        return false;
    if (isByteLike(value))
        return false;
    const proto = Object.getPrototypeOf(value);
    return proto === Object.prototype || proto === null;
}
function numberCanBeFloat32(value) {
    return Number.isFinite(value) && Math.fround(value) === value;
}
function escapeKey(value) {
    return value.replace(/([\\|:,\[\]])/g, "\\$1");
}
class ByteWriter {
    chunks = [];
    totalLength = 0;
    pushByte(value) {
        const bytes = new Uint8Array(1);
        bytes[0] = value & 0xff;
        this.chunks.push(bytes);
        this.totalLength += 1;
    }
    pushBytes(value) {
        const bytes = value instanceof Uint8Array ? value : Uint8Array.from(value);
        if (bytes.length === 0)
            return;
        this.chunks.push(bytes);
        this.totalLength += bytes.length;
    }
    toUint8Array() {
        const output = new Uint8Array(this.totalLength);
        let offset = 0;
        for (const chunk of this.chunks) {
            output.set(chunk, offset);
            offset += chunk.length;
        }
        return output;
    }
}
class Reader {
    data;
    view;
    index = 0;
    constructor(data) {
        this.data = data;
        this.view = new DataView(data.buffer, data.byteOffset, data.byteLength);
    }
    remaining() {
        return this.data.length - this.index;
    }
    position() {
        return this.index;
    }
    ensureAvailable(byteCount, context) {
        if (byteCount < 0)
            throw new RangeError(`${context}: byteCount cannot be negative.`);
        if (byteCount > this.remaining()) {
            throw new Error(`${context} exceeds remaining input. Need ${byteCount} byte(s), remaining=${this.remaining()}.`);
        }
    }
    checkedLength(length, context) {
        if (!Number.isInteger(length) || length < 0) {
            throw new Error(`${context} is invalid: ${length}.`);
        }
        if (length > this.remaining()) {
            throw new Error(`${context} exceeds remaining input. Length=${length}, remaining=${this.remaining()}.`);
        }
        return length;
    }
    checkedCount(count, minBytesPerItem, context) {
        if (!Number.isInteger(count) || count < 0) {
            throw new Error(`${context} is invalid: ${count}.`);
        }
        if (minBytesPerItem <= 0) {
            throw new RangeError(`${context}: minBytesPerItem must be > 0.`);
        }
        if (count > 0 && count > Math.floor(this.remaining() / minBytesPerItem)) {
            throw new Error(`${context} is too large for remaining input. Count=${count}, remaining=${this.remaining()}, minBytesPerItem=${minBytesPerItem}.`);
        }
        return count;
    }
    peekByte() {
        this.ensureAvailable(1, "PeekByte");
        return this.data[this.index];
    }
    readByte() {
        this.ensureAvailable(1, "ReadByte");
        return this.data[this.index++];
    }
    skip(byteCount, context) {
        this.ensureAvailable(byteCount, context);
        this.index += byteCount;
    }
    readUInt16BE() {
        this.ensureAvailable(2, "ReadUInt16BE");
        const value = this.view.getUint16(this.index, false);
        this.index += 2;
        return value;
    }
    readUInt24BE() {
        this.ensureAvailable(3, "ReadUInt24BE");
        const o = this.index;
        const value = (this.data[o] << 16) | (this.data[o + 1] << 8) | this.data[o + 2];
        this.index += 3;
        return value >>> 0;
    }
    readUInt32BE() {
        this.ensureAvailable(4, "ReadUInt32BE");
        const value = this.view.getUint32(this.index, false);
        this.index += 4;
        return value;
    }
    readUInt40BE() {
        this.ensureAvailable(5, "ReadUInt40BE");
        const o = this.index;
        const value = this.data[o] * 0x100000000 +
            ((this.data[o + 1] << 24) >>> 0) +
            (this.data[o + 2] << 16) +
            (this.data[o + 3] << 8) +
            this.data[o + 4];
        this.index += 5;
        return value;
    }
    readUInt48BE() {
        this.ensureAvailable(6, "ReadUInt48BE");
        let value = 0;
        for (let i = 0; i < 6; i++) {
            value = value * 256 + this.data[this.index + i];
        }
        this.index += 6;
        return value;
    }
    readUInt56BE() {
        this.ensureAvailable(7, "ReadUInt56BE");
        let value = 0;
        for (let i = 0; i < 7; i++) {
            value = value * 256 + this.data[this.index + i];
        }
        this.index += 7;
        return value;
    }
    readUInt64BE() {
        this.ensureAvailable(8, "ReadUInt64BE");
        let value = 0n;
        for (let i = 0; i < 8; i++) {
            value = (value << 8n) | BigInt(this.data[this.index++]);
        }
        return value;
    }
    readLUintOrNull() {
        const first = this.readByte();
        switch (first) {
            case LUINT_NULL:
                return null;
            case LUINT_B8:
                return LUINT_B8_BASE_VALUE + this.readByte();
            case LUINT_16:
                return this.readUInt16BE();
            case LUINT_24:
                return this.readUInt24BE();
            case LUINT_32:
                return this.readUInt32BE();
            case LUINT_40:
                return this.readUInt40BE();
            case LUINT_48:
                return this.readUInt48BE();
            case LUINT_56:
                return this.readUInt56BE();
            case LUINT_64: {
                const value = this.readUInt64BE();
                if (value > BigInt(Number.MAX_SAFE_INTEGER)) {
                    throw new Error(`LUINT too large for JavaScript number infrastructure: ${value.toString()}.`);
                }
                return Number(value);
            }
            default:
                if (first <= LUINT_B0_MAX)
                    return first;
                throw new Error(`Invalid LUINT first byte: 0x${first.toString(16).padStart(2, "0")}`);
        }
    }
    readLUintRequired() {
        const value = this.readLUintOrNull();
        if (value === null)
            throw new Error("LUINT is NULL where a value is required.");
        return value;
    }
    readLIntOrNull() {
        const tag = this.readByte();
        if (tag <= LINT_POS_LITERAL_MAX_VALUE)
            return tag;
        if (tag >= LINT_NEG_LITERAL_FIRST_TAG && tag <= LINT_NEG_LITERAL_LAST_TAG) {
            return -(tag - LINT_POS_LITERAL_MAX_VALUE);
        }
        switch (tag) {
            case LINT_POS_PLUS_U8_TAG:
                return LINT_POS_PLUS_U8_BASE_VALUE + this.readByte();
            case LINT_POS_U16_TAG:
                return this.readUInt16BE();
            case LINT_POS_U24_TAG:
                return this.readUInt24BE();
            case LINT_POS_U32_TAG:
                return this.readUInt32BE();
            case LINT_POS_U40_TAG:
                return losslessIntegerFromBigInt(BigInt(this.readUInt40BE()));
            case LINT_POS_U48_TAG:
                return losslessIntegerFromBigInt(BigInt(this.readUInt48BE()));
            case LINT_POS_U56_TAG:
                return losslessIntegerFromBigInt(BigInt(this.readUInt56BE()));
            case LINT_POS_U64_TAG:
                return losslessIntegerFromBigInt(this.readUInt64BE());
            case LINT_NEG_PLUS_U8_TAG:
                return -(LINT_NEG_PLUS_U8_BASE_MAG + this.readByte());
            case LINT_NEG_U16_TAG:
                return -this.readUInt16BE();
            case LINT_NEG_U24_TAG:
                return -this.readUInt24BE();
            case LINT_NEG_U32_TAG:
                return -this.readUInt32BE();
            case LINT_NEG_U40_TAG:
                return losslessIntegerFromBigInt(-BigInt(this.readUInt40BE()));
            case LINT_NEG_U48_TAG:
                return losslessIntegerFromBigInt(-BigInt(this.readUInt48BE()));
            case LINT_NEG_U56_TAG:
                return losslessIntegerFromBigInt(-BigInt(this.readUInt56BE()));
            case LINT_NEG_U64_TAG: {
                const mag = this.readUInt64BE();
                if (mag === 0x8000000000000000n)
                    return -(1n << 63n);
                return losslessIntegerFromBigInt(-mag);
            }
            case LINT_NULL_TAG:
                return null;
            default:
                throw new Error(`Invalid LINT tag: 0x${tag.toString(16).padStart(2, "0")}`);
        }
    }
    readBoolOrNull() {
        const b = this.readByte();
        switch (b) {
            case BOOL_FALSE:
                return false;
            case BOOL_TRUE:
                return true;
            case BOOL_NULL:
                return null;
            default:
                throw new Error(`Invalid BOOL payload: 0x${b.toString(16).padStart(2, "0")}`);
        }
    }
    readSingleOrNull() {
        this.ensureAvailable(2, "ReadSingleOrNull");
        if (this.data[this.index] === FLOAT_NULL_SENTINEL_B0 &&
            this.data[this.index + 1] === FLOAT_NULL_SENTINEL_B1) {
            this.index += 2;
            return null;
        }
        this.ensureAvailable(4, "ReadSingleOrNull");
        const start = this.index;
        this.index += 4;
        return this.view.getFloat32(start, false);
    }
    readDoubleOrNull() {
        this.ensureAvailable(2, "ReadDoubleOrNull");
        if (this.data[this.index] === FLOAT_NULL_SENTINEL_B0 &&
            this.data[this.index + 1] === FLOAT_NULL_SENTINEL_B1) {
            this.index += 2;
            return null;
        }
        this.ensureAvailable(8, "ReadDoubleOrNull");
        const start = this.index;
        this.index += 8;
        return this.view.getFloat64(start, false);
    }
    readSchemaPointerIndex() {
        const tag = this.readByte();
        return this.readSchemaPointerIndexFromFirst(tag);
    }
    readSchemaPointerIndexFromFirst(firstTag) {
        switch (firstTag) {
            case SOBJ_SCHEMA_PTR_1B_TAG:
                return this.readByte();
            case SOBJ_SCHEMA_PTR_2B_TAG:
                return this.readUInt16BE();
            case SOBJ_SCHEMA_PTR_3B_TAG:
                return this.readUInt24BE();
            default:
                throw new Error(`Invalid schema pointer tag: 0x${firstTag.toString(16).padStart(2, "0")}`);
        }
    }
    readUtf8Literal(length) {
        if (length < 0)
            throw new RangeError("UTF-8 literal length cannot be negative.");
        if (length === 0)
            return "";
        this.ensureAvailable(length, "UTF-8 literal");
        const start = this.index;
        this.index += length;
        return UTF8_DECODER.decode(this.data.subarray(start, start + length));
    }
    readBytesLiteral(length) {
        if (length < 0)
            throw new RangeError("Byte literal length cannot be negative.");
        if (length === 0)
            return new Uint8Array(0);
        this.ensureAvailable(length, "Byte literal");
        const start = this.index;
        this.index += length;
        return this.data.slice(start, start + length);
    }
    readLStrOrNull() {
        const length = this.readLUintOrNull();
        if (length === null)
            return null;
        return this.readUtf8Literal(this.checkedLength(length, "LSTR length"));
    }
    readLStrRequired() {
        const value = this.readLStrOrNull();
        if (value === null)
            throw new Error("LSTR is NULL where a value is required.");
        return value;
    }
    readDStrChunk() {
        const tag = this.readByte();
        if (tag === DSTR_NULL_TAG) {
            return { literal: null, ptrIndex: -1, isNull: true };
        }
        if (tag <= DSTR_LEN_LITERAL_MAX) {
            return { literal: this.readUtf8Literal(tag), ptrIndex: -1, isNull: false };
        }
        switch (tag) {
            case DSTR_LEN_U8_TAG:
                return {
                    literal: this.readUtf8Literal(DSTR_LEN_U8_BASE + this.readByte()),
                    ptrIndex: -1,
                    isNull: false,
                };
            case DSTR_LEN_U16_TAG:
                return { literal: this.readUtf8Literal(this.readUInt16BE()), ptrIndex: -1, isNull: false };
            case DSTR_LEN_U24_TAG:
                return { literal: this.readUtf8Literal(this.readUInt24BE()), ptrIndex: -1, isNull: false };
            case DSTR_LEN_U32_TAG: {
                const length = this.readUInt32BE();
                return { literal: this.readUtf8Literal(this.checkedLength(length, "DSTR length")), ptrIndex: -1, isNull: false };
            }
            case DSTR_PTR_U8_TAG:
                return { literal: null, ptrIndex: DSTR_PTR_LITERAL_COUNT + this.readByte(), isNull: false };
            case DSTR_PTR_U16_TAG:
                return { literal: null, ptrIndex: this.readUInt16BE(), isNull: false };
            case DSTR_PTR_U24_TAG:
                return { literal: null, ptrIndex: this.readUInt24BE(), isNull: false };
            case DSTR_PTR_U32_TAG:
                return { literal: null, ptrIndex: this.readUInt32BE(), isNull: false };
            default:
                if (tag >= DSTR_PTR_LITERAL_BASE_TAG && tag <= DSTR_PTR_LITERAL_MAX_TAG) {
                    return { literal: null, ptrIndex: tag - DSTR_PTR_LITERAL_BASE_TAG, isNull: false };
                }
                throw new Error(`Invalid DSTR tag: 0x${tag.toString(16).padStart(2, "0")}`);
        }
    }
    readBarrOrNull() {
        const length = this.readLUintOrNull();
        if (length === null)
            return null;
        return this.readBytesLiteral(this.checkedLength(length, "BARR length"));
    }
    readDDate() {
        const tag = this.readByte();
        if (tag === DDATE_NULL_TAG) {
            return { literalUtc: null, ptrIndex: -1, isNull: true };
        }
        if (tag <= LDATE_TICKS_B0_MAX) {
            const ticks = (BigInt(tag) << 56n) |
                (BigInt(this.readByte()) << 48n) |
                (BigInt(this.readByte()) << 40n) |
                (BigInt(this.readByte()) << 32n) |
                (BigInt(this.readByte()) << 24n) |
                (BigInt(this.readByte()) << 16n) |
                (BigInt(this.readByte()) << 8n) |
                BigInt(this.readByte());
            return { literalUtc: ticksToJsDate(ticks), ptrIndex: -1, isNull: false };
        }
        if (tag >= DDATE_PTR_LITERAL_BASE_TAG && tag <= DDATE_PTR_LITERAL_MAX_TAG) {
            return { literalUtc: null, ptrIndex: tag - DDATE_PTR_LITERAL_BASE_TAG, isNull: false };
        }
        switch (tag) {
            case DDATE_PTR_U8_TAG:
                return { literalUtc: null, ptrIndex: DDATE_PTR_U8_BASE_INDEX + this.readByte(), isNull: false };
            case DDATE_PTR_U16_TAG:
                return { literalUtc: null, ptrIndex: this.readUInt16BE(), isNull: false };
            case DDATE_PTR_U24_TAG:
                return { literalUtc: null, ptrIndex: this.readUInt24BE(), isNull: false };
            case DDATE_PTR_U32_TAG:
                return { literalUtc: null, ptrIndex: this.readUInt32BE(), isNull: false };
            default:
                throw new Error(`Invalid DDATE tag: 0x${tag.toString(16).padStart(2, "0")}`);
        }
    }
    skipHeader() {
        const pairCount = this.readLUintOrNull();
        if (pairCount === null)
            return;
        const headerBytes = this.checkedLength(this.readLUintRequired(), "Header byteLength");
        if (headerBytes > 0) {
            this.skip(headerBytes, "Header body");
        }
    }
    skipFilesZone() {
        const count = this.checkedCount(this.readLUintRequired(), 1, "Files count");
        for (let i = 0; i < count; i++) {
            const name = this.readLStrOrNull();
            if (name === null)
                throw new Error(`Files zone contains a NULL file name at index ${i}.`);
            const payloadBytes = this.checkedLength(this.readLUintRequired(), `File[${i}] payload length`);
            if (payloadBytes > 0) {
                this.skip(payloadBytes, `File[${i}] payload`);
            }
        }
    }
}
class Decoder {
    reader;
    zoneMask = 0;
    strings = [];
    dates = [];
    schemas = [];
    constructor(source) {
        this.reader = new Reader(source);
    }
    decode() {
        this.readMagic();
        this.readZoneMask();
        if (this.hasZone(ZMSK_HEADERS)) {
            this.reader.skipHeader();
        }
        if (this.hasZone(ZMSK_FILES)) {
            this.reader.skipFilesZone();
        }
        if (this.hasZone(ZMSK_STRING_TABLE)) {
            this.readStringTable();
        }
        else {
            this.strings = [];
        }
        if (this.hasZone(ZMSK_DATE_TABLE)) {
            this.readDateTable();
        }
        else {
            this.dates = [];
        }
        if (this.hasZone(ZMSK_SCHEMA_TABLE)) {
            this.readSchemas();
        }
        else {
            this.schemas = [];
        }
        return this.hasZone(ZMSK_DATA) ? this.readRoot() : null;
    }
    readMagic() {
        const b0 = this.reader.readByte();
        const b1 = this.reader.readByte();
        const b2 = this.reader.readByte();
        const b3 = this.reader.readByte();
        if (b0 !== FILE_MAGIC_B0 || b1 !== FILE_MAGIC_B1 || b2 !== FILE_MAGIC_B2 || b3 !== FILE_MAGIC_B3) {
            throw new Error(`Invalid file magic. Expected BYT1, got 0x${b0.toString(16).padStart(2, "0")} 0x${b1.toString(16).padStart(2, "0")} 0x${b2.toString(16).padStart(2, "0")} 0x${b3.toString(16).padStart(2, "0")}.`);
        }
        const version = this.reader.readByte();
        if (version !== FILE_VERSION_V1) {
            throw new Error(`Unsupported file version: ${version}`);
        }
    }
    readZoneMask() {
        const zoneMask = this.reader.readByte();
        if ((zoneMask & ZMSK_HAS_NEXT) !== 0) {
            throw new Error("Unsupported ZMSK chain: continuation bit is set, but this decoder supports only one ZMSK byte.");
        }
        const unsupported = (zoneMask & ~ZMSK_V1_DEFINED_ZONES_MASK) & 0xff;
        if (unsupported !== 0) {
            throw new Error(`Unsupported ZMSK bits for this decoder: 0x${unsupported.toString(16).padStart(2, "0")}`);
        }
        this.zoneMask = zoneMask & ZMSK_ZONE_BITS_MASK;
    }
    hasZone(bit) {
        return (this.zoneMask & bit) === bit;
    }
    readStringTable() {
        const count = this.reader.checkedCount(this.reader.readLUintRequired(), 1, "String table count");
        if (count === 0) {
            this.strings = [];
            return;
        }
        const table = new Array(count);
        for (let i = 0; i < count; i++) {
            const value = this.reader.readLStrOrNull();
            if (value === null)
                throw new Error("String table cannot contain NULL entries.");
            table[i] = value;
        }
        this.strings = table;
    }
    readDateTable() {
        const count = this.reader.checkedCount(this.reader.readLUintRequired(), 8, "Date table count");
        if (count === 0) {
            this.dates = [];
            return;
        }
        const table = new Array(count);
        for (let i = 0; i < count; i++) {
            const entry = this.reader.readDDate();
            if (entry.isNull || entry.ptrIndex !== -1 || entry.literalUtc === null) {
                throw new Error("Date table cannot contain NULL entries or pointers.");
            }
            table[i] = entry.literalUtc;
        }
        this.dates = table;
    }
    readSchemas() {
        const count = this.reader.checkedCount(this.reader.readLUintRequired(), 1, "Schema count");
        const schemas = new Array(count);
        for (let i = 0; i < count; i++) {
            const smat = this.reader.readByte();
            if (smat === SMAT_NULL_TAG) {
                schemas[i] = null;
            }
            else if (smat >= SMAT_MAP_BASE_TAG && smat <= SMAT_ARR_MAX_TAG) {
                schemas[i] = this.readNonObjectSchema(smat, i);
            }
            else {
                schemas[i] = this.readObjectSchema(smat, i);
            }
        }
        this.schemas = schemas;
    }
    readNonObjectSchema(smat, index) {
        let typeId;
        let isMapArray = false;
        let kind;
        if (smat >= SMAT_MAP_BASE_TAG && smat <= SMAT_MAP_MAX_TAG) {
            kind = 2 /* SchemaKind.Map */;
            typeId = smat - SMAT_MAP_BASE_TAG + 1;
        }
        else if (smat >= SMAT_MAP_ARR_BASE_TAG && smat <= SMAT_MAP_ARR_MAX_TAG) {
            kind = 2 /* SchemaKind.Map */;
            isMapArray = true;
            typeId = smat - SMAT_MAP_ARR_BASE_TAG + 1;
        }
        else if (smat >= SMAT_ARR_BASE_TAG && smat <= SMAT_ARR_MAX_TAG) {
            kind = 3 /* SchemaKind.Array */;
            typeId = smat - SMAT_ARR_BASE_TAG + 1;
        }
        else {
            throw new Error(`Invalid non-object SMAT: 0x${smat.toString(16).padStart(2, "0")}`);
        }
        if (typeId < 1 /* FieldType.Integer */ || typeId > 8 /* FieldType.Object */) {
            throw new Error(`Invalid SMAT typeId: ${typeId}`);
        }
        const schemaRef = typeId === 8 /* FieldType.Object */ ? this.reader.readSchemaPointerIndex() : -1;
        if (kind === 2 /* SchemaKind.Map */) {
            return {
                key: `decoded-map-${index}`,
                kind,
                index,
                valueType: isMapArray ? (typeId | 128 /* FieldType.ArrayFlag */) : typeId,
                valueSchemaKey: null,
            };
        }
        return {
            key: `decoded-array-${index}`,
            kind,
            index,
            elemType: typeId,
            elemSchemaKey: schemaRef >= 0 ? `@schema:${schemaRef}` : null,
        };
    }
    readObjectSchema(firstSmat, index) {
        let fieldCount;
        if (firstSmat <= SMAT_OBJ_LITERAL_MAX_FIELDS) {
            fieldCount = firstSmat;
        }
        else if (firstSmat === SMAT_OBJ_LUINT_TAG) {
            fieldCount = this.reader.checkedCount(this.reader.readLUintRequired(), 2, "Object fieldCount");
        }
        else {
            throw new Error(`Invalid SMAT for object schema: 0x${firstSmat.toString(16).padStart(2, "0")}`);
        }
        const fields = new Array(fieldCount);
        for (let i = 0; i < fieldCount; i++) {
            const typeCode = this.reader.readByte();
            const nameChunk = this.reader.readDStrChunk();
            if (nameChunk.isNull)
                throw new Error("Field name cannot be null.");
            let fieldName = nameChunk.literal;
            if (fieldName === null) {
                const idx = nameChunk.ptrIndex;
                if (idx < 0 || idx >= this.strings.length) {
                    throw new Error(`Invalid field-name string pointer index: ${idx}`);
                }
                fieldName = this.strings[idx];
            }
            if (fieldName === null)
                throw new Error("Field name cannot be null.");
            let refSchemaKey = null;
            if (baseType(typeCode) === 8 /* FieldType.Object */) {
                const schemaIndex = this.reader.readSchemaPointerIndex();
                refSchemaKey = `@schema:${schemaIndex}`;
            }
            fields[i] = { name: fieldName, typeCode, refSchemaKey };
        }
        return {
            key: `decoded-object-${index}`,
            kind: 1 /* SchemaKind.Object */,
            index,
            fields,
        };
    }
    readRoot() {
        const first = this.reader.peekByte();
        if (first >= SOBJ_SCHEMA_PTR_1B_TAG && first <= SOBJ_SCHEMA_PTR_3B_TAG) {
            const rootSchemaIndex = this.reader.readSchemaPointerIndex();
            const rootSchema = this.requireSchemaByIndex(rootSchemaIndex, "root schema");
            switch (rootSchema.kind) {
                case 2 /* SchemaKind.Map */:
                    return this.readMapBody(rootSchema, rootSchemaIndex);
                case 3 /* SchemaKind.Array */:
                    return this.readArray(rootSchema.elemType, this.getSchemaIndexFromRef(rootSchema.elemSchemaKey, "root array elem schema"));
                case 1 /* SchemaKind.Object */:
                    return this.readObjectValue(rootSchema, rootSchemaIndex);
            }
        }
        if (first === SOBJ_NULL_TAG) {
            this.reader.readByte();
            return null;
        }
        const rootTypeCode = this.reader.readByte();
        const rootBaseType = baseType(rootTypeCode);
        if (rootBaseType < 1 /* FieldType.Integer */ || rootBaseType > 7 /* FieldType.Bytes */) {
            throw new Error(`Invalid root type tag: 0x${rootTypeCode.toString(16).padStart(2, "0")}`);
        }
        return isArrayType(rootTypeCode)
            ? this.readArray(rootBaseType, -1)
            : this.readScalar(rootBaseType, -1);
    }
    requireSchemaByIndex(index, context) {
        if (index < 0 || index >= this.schemas.length) {
            throw new Error(`Invalid ${context} index: ${index}`);
        }
        const schema = this.schemas[index];
        if (schema === null) {
            throw new Error(`${context} points to NULL entry: ${index}`);
        }
        return schema;
    }
    getSchemaIndexFromRef(refSchemaKey, context) {
        if (refSchemaKey === null)
            return -1;
        if (!refSchemaKey.startsWith("@schema:")) {
            throw new Error(`Invalid ${context} reference: ${refSchemaKey}`);
        }
        const value = Number(refSchemaKey.slice(8));
        if (!Number.isInteger(value) || value < 0) {
            throw new Error(`Invalid ${context} reference: ${refSchemaKey}`);
        }
        return value;
    }
    readSchemaBody(schema, schemaIndex) {
        switch (schema.kind) {
            case 2 /* SchemaKind.Map */:
                return this.readMapBody(schema, schemaIndex);
            case 3 /* SchemaKind.Array */:
                return this.readArray(schema.elemType, this.getSchemaIndexFromRef(schema.elemSchemaKey, "array schema elem"));
            case 1 /* SchemaKind.Object */:
                return this.readObjectBody(schema);
        }
    }
    readObjectBody(schema) {
        const obj = {};
        for (const field of schema.fields) {
            const schemaIndex = this.getSchemaIndexFromRef(field.refSchemaKey, `field ${field.name} schema`);
            obj[field.name] = this.readValue(field.typeCode, schemaIndex);
        }
        return obj;
    }
    readObjectValue(expected, expectedIndex) {
        const marker = this.reader.readByte();
        switch (marker) {
            case SOBJ_NULL_TAG:
                return null;
            case SOBJ_PRESENT_EXPECTED_SCHEMA:
                return this.readSchemaBody(expected, expectedIndex);
            case SOBJ_SCHEMA_PTR_1B_TAG:
            case SOBJ_SCHEMA_PTR_2B_TAG:
            case SOBJ_SCHEMA_PTR_3B_TAG: {
                const idx = this.reader.readSchemaPointerIndexFromFirst(marker);
                const schema = this.requireSchemaByIndex(idx, "schema override");
                return this.readSchemaBody(schema, idx);
            }
            default: {
                const typeCode = marker;
                const valueBaseType = baseType(typeCode);
                if (valueBaseType === 8 /* FieldType.Object */) {
                    if (!isArrayType(typeCode)) {
                        throw new Error("Invalid SOBJ marker: Object scalar override is not allowed.");
                    }
                    return this.readObjectArrayOverrideExpected(expectedIndex);
                }
                return isArrayType(typeCode)
                    ? this.readArray(valueBaseType, -1)
                    : this.readScalar(valueBaseType, -1);
            }
        }
    }
    readObjectArrayOverrideExpected(expectedSchemaIndex) {
        const countOrNull = this.reader.readLUintOrNull();
        if (countOrNull === null)
            return null;
        const count = this.reader.checkedCount(countOrNull, 1, "Object[] count");
        const expected = this.requireSchemaByIndex(expectedSchemaIndex, "Object[] expected schema");
        const out = new Array(count);
        for (let i = 0; i < count; i++) {
            out[i] = this.readObjectValue(expected, expectedSchemaIndex);
        }
        return out;
    }
    readMapBody(schema, _schemaIndex) {
        const count = this.reader.checkedCount(this.reader.readLUintRequired(), 2, "Map count");
        const map = new Map();
        const valueSchemaIndex = this.getSchemaIndexFromRef(schema.valueSchemaKey, "map value schema");
        for (let i = 0; i < count; i++) {
            const key = this.readDStrValue();
            map.set(key ?? "null", this.readValue(schema.valueType, valueSchemaIndex));
        }
        return map;
    }
    readValue(typeCode, schemaIndex) {
        return isArrayType(typeCode)
            ? this.readArray(baseType(typeCode), schemaIndex)
            : this.readScalar(baseType(typeCode), schemaIndex);
    }
    readArray(base, schemaIndex) {
        const countOrNull = this.reader.readLUintOrNull();
        if (countOrNull === null)
            return null;
        let count;
        switch (base) {
            case 1 /* FieldType.Integer */:
                count = this.reader.checkedCount(countOrNull, 1, "Integer[] count");
                break;
            case 2 /* FieldType.Float4Bytes */:
                count = this.reader.checkedCount(countOrNull, 2, "Float4[] count");
                break;
            case 3 /* FieldType.Float8Bytes */:
                count = this.reader.checkedCount(countOrNull, 2, "Float8[] count");
                break;
            case 4 /* FieldType.Boolean */:
                count = this.reader.checkedCount(countOrNull, 1, "Boolean[] count");
                break;
            case 5 /* FieldType.Date */:
                count = this.reader.checkedCount(countOrNull, 1, "Date[] count");
                break;
            case 6 /* FieldType.String */:
                count = this.reader.checkedCount(countOrNull, 1, "String[] count");
                break;
            case 7 /* FieldType.Bytes */:
                count = this.reader.checkedCount(countOrNull, 1, "Bytes[] count");
                break;
            case 8 /* FieldType.Object */:
                count = this.reader.checkedCount(countOrNull, 1, "Object[] count");
                break;
            default:
                throw new Error(`Unsupported array base type: ${base}`);
        }
        const out = new Array(count);
        switch (base) {
            case 1 /* FieldType.Integer */:
                for (let i = 0; i < count; i++)
                    out[i] = this.reader.readLIntOrNull();
                return out;
            case 2 /* FieldType.Float4Bytes */:
                for (let i = 0; i < count; i++)
                    out[i] = this.reader.readSingleOrNull();
                return out;
            case 3 /* FieldType.Float8Bytes */:
                for (let i = 0; i < count; i++)
                    out[i] = this.reader.readDoubleOrNull();
                return out;
            case 4 /* FieldType.Boolean */:
                for (let i = 0; i < count; i++)
                    out[i] = this.reader.readBoolOrNull();
                return out;
            case 5 /* FieldType.Date */:
                for (let i = 0; i < count; i++)
                    out[i] = this.readDateValueUtcOrNull();
                return out;
            case 6 /* FieldType.String */:
                for (let i = 0; i < count; i++)
                    out[i] = this.readDStrValue();
                return out;
            case 7 /* FieldType.Bytes */:
                for (let i = 0; i < count; i++)
                    out[i] = this.reader.readBarrOrNull();
                return out;
            case 8 /* FieldType.Object */: {
                const expected = this.requireSchemaByIndex(schemaIndex, "Object[] expected schema");
                for (let i = 0; i < count; i++)
                    out[i] = this.readObjectValue(expected, schemaIndex);
                return out;
            }
            default:
                throw new Error(`Unsupported array base type: ${base}`);
        }
    }
    readDateValueUtcOrNull() {
        const entry = this.reader.readDDate();
        if (entry.isNull)
            return null;
        if (entry.ptrIndex >= 0) {
            if (entry.ptrIndex >= this.dates.length) {
                throw new Error(`Invalid DDATE pointer index: ${entry.ptrIndex}`);
            }
            return this.dates[entry.ptrIndex];
        }
        return entry.literalUtc;
    }
    readScalar(base, schemaIndex) {
        switch (base) {
            case 1 /* FieldType.Integer */:
                return this.reader.readLIntOrNull();
            case 4 /* FieldType.Boolean */:
                return this.reader.readBoolOrNull();
            case 2 /* FieldType.Float4Bytes */:
                return this.reader.readSingleOrNull();
            case 3 /* FieldType.Float8Bytes */:
                return this.reader.readDoubleOrNull();
            case 5 /* FieldType.Date */:
                return this.readDateValueUtcOrNull();
            case 6 /* FieldType.String */:
                return this.readDStrValue();
            case 7 /* FieldType.Bytes */:
                return this.reader.readBarrOrNull();
            case 8 /* FieldType.Object */: {
                const expected = this.requireSchemaByIndex(schemaIndex, "Object expected schema");
                return this.readObjectValue(expected, schemaIndex);
            }
            default:
                throw new Error(`Unsupported scalar FieldType: ${base}`);
        }
    }
    readDStrValue() {
        const chunk = this.reader.readDStrChunk();
        if (chunk.isNull)
            return null;
        if (chunk.ptrIndex >= 0) {
            if (chunk.ptrIndex >= this.strings.length) {
                throw new Error(`Invalid DSTR pointer index: ${chunk.ptrIndex}`);
            }
            return this.strings[chunk.ptrIndex];
        }
        return chunk.literal;
    }
}
class Encoder {
    strings = [];
    stringIndexByValue = new Map();
    dateRows = [];
    datePointerByTicks = new Map();
    seenDateTicksOnce = new Set();
    schemas = [];
    schemaIndexByKey = new Map();
    analyzingRefs = new Set();
    encodingRefs = new Set();
    emptyObjectSchemaKey = null;
    encode(value) {
        const root = this.analyzeValue(value);
        const dataWriter = new ByteWriter();
        this.writeRoot(root, value, dataWriter);
        const schemaWriter = new ByteWriter();
        this.writeSchemaTable(schemaWriter);
        const stringWriter = new ByteWriter();
        this.writeStringTable(stringWriter);
        const dateWriter = new ByteWriter();
        this.writeDateTable(dateWriter);
        const zoneMask = this.getZoneMask(true);
        const out = new ByteWriter();
        out.pushBytes([FILE_MAGIC_B0, FILE_MAGIC_B1, FILE_MAGIC_B2, FILE_MAGIC_B3, FILE_VERSION_V1, zoneMask]);
        if ((zoneMask & ZMSK_STRING_TABLE) !== 0) {
            out.pushBytes(stringWriter.toUint8Array());
        }
        if ((zoneMask & ZMSK_DATE_TABLE) !== 0) {
            out.pushBytes(dateWriter.toUint8Array());
        }
        if ((zoneMask & ZMSK_SCHEMA_TABLE) !== 0) {
            out.pushBytes(schemaWriter.toUint8Array());
        }
        if ((zoneMask & ZMSK_DATA) !== 0) {
            out.pushBytes(dataWriter.toUint8Array());
        }
        return out.toUint8Array();
    }
    getZoneMask(includeData) {
        let mask = 0;
        if (this.strings.length !== 0)
            mask |= ZMSK_STRING_TABLE;
        if (this.dateRows.length !== 0)
            mask |= ZMSK_DATE_TABLE;
        if (this.schemas.length !== 0)
            mask |= ZMSK_SCHEMA_TABLE;
        if (includeData)
            mask |= ZMSK_DATA;
        return mask;
    }
    analyzeValue(value) {
        if (value === null || value === undefined) {
            return { category: "complex", schemaKey: this.ensureEmptyObjectSchema() };
        }
        if (typeof value === "boolean") {
            return { category: "primitive", typeCode: 4 /* FieldType.Boolean */ };
        }
        if (typeof value === "string") {
            return { category: "primitive", typeCode: 6 /* FieldType.String */ };
        }
        if (value instanceof Date) {
            return { category: "primitive", typeCode: 5 /* FieldType.Date */ };
        }
        if (isByteLike(value)) {
            return { category: "primitive", typeCode: 7 /* FieldType.Bytes */ };
        }
        if (typeof value === "bigint") {
            return { category: "primitive", typeCode: 1 /* FieldType.Integer */ };
        }
        if (typeof value === "number") {
            if (Number.isInteger(value)) {
                return { category: "primitive", typeCode: 1 /* FieldType.Integer */ };
            }
            return { category: "primitive", typeCode: numberCanBeFloat32(value) ? 2 /* FieldType.Float4Bytes */ : 3 /* FieldType.Float8Bytes */ };
        }
        if (Array.isArray(value)) {
            return this.analyzeArray(value);
        }
        if (value instanceof Map) {
            return this.analyzeMap(value);
        }
        if (isPlainObject(value)) {
            return this.analyzeObject(value);
        }
        throw new Error(`Unsupported value type for Bytery.encode: ${Object.prototype.toString.call(value)}`);
    }
    analyzeSlotValue(value) {
        if (value === null || value === undefined) {
            const emptyKey = this.ensureEmptyObjectSchema();
            return {
                typeCode: 8 /* FieldType.Object */,
                refSchemaKey: emptyKey,
                schemaKey: emptyKey,
                isPrimitive: false,
                isArray: false,
                isNull: true,
            };
        }
        const analysis = this.analyzeValue(value);
        if (analysis.category === "primitive") {
            return {
                typeCode: analysis.typeCode,
                refSchemaKey: null,
                schemaKey: null,
                isPrimitive: true,
                isArray: isArrayType(analysis.typeCode),
                isNull: false,
            };
        }
        const schema = this.requireSchema(analysis.schemaKey);
        if (schema.kind === 3 /* SchemaKind.Array */) {
            return {
                typeCode: 8 /* FieldType.Object */ | 128 /* FieldType.ArrayFlag */,
                refSchemaKey: schema.elemSchemaKey ?? this.ensureEmptyObjectSchema(),
                schemaKey: analysis.schemaKey,
                isPrimitive: false,
                isArray: true,
                isNull: false,
            };
        }
        return {
            typeCode: 8 /* FieldType.Object */,
            refSchemaKey: analysis.schemaKey,
            schemaKey: analysis.schemaKey,
            isPrimitive: false,
            isArray: false,
            isNull: false,
        };
    }
    analyzeObject(value) {
        this.enterAnalyzingRef(value, "Encode analysis cycle detected in object.");
        try {
            const keys = Object.keys(value).sort();
            const fields = new Array(keys.length);
            for (let i = 0; i < keys.length; i++) {
                const key = keys[i];
                const slot = this.analyzeSlotValue(value[key]);
                fields[i] = {
                    name: key,
                    typeCode: slot.typeCode,
                    refSchemaKey: baseType(slot.typeCode) === 8 /* FieldType.Object */ ? slot.refSchemaKey : null,
                };
            }
            const schemaKey = "obj-" +
                fields
                    .map((field) => `${escapeKey(field.name)}:${field.typeCode}:${field.refSchemaKey ?? ""}`)
                    .join("-");
            this.ensureObjectSchema(schemaKey, fields);
            return { category: "complex", schemaKey };
        }
        finally {
            this.exitAnalyzingRef(value);
        }
    }
    analyzeMap(value) {
        this.enterAnalyzingRef(value, "Encode analysis cycle detected in map.");
        try {
            const descriptors = [];
            for (const [, mapValue] of value) {
                if (mapValue === null || mapValue === undefined)
                    continue;
                descriptors.push(this.analyzeSlotValue(mapValue));
            }
            let valueType = 8 /* FieldType.Object */;
            let valueSchemaKey = this.ensureEmptyObjectSchema();
            if (descriptors.length > 0) {
                const allArrays = descriptors.every((d) => d.isArray);
                const allScalars = descriptors.every((d) => !d.isArray);
                if (allArrays) {
                    const firstType = descriptors[0].typeCode;
                    const sameType = descriptors.every((d) => d.typeCode === firstType);
                    const allPrimitiveArrays = descriptors.every((d) => d.isPrimitive && d.isArray);
                    if (allPrimitiveArrays && sameType) {
                        valueType = firstType;
                        valueSchemaKey = null;
                    }
                    else {
                        valueType = 8 /* FieldType.Object */ | 128 /* FieldType.ArrayFlag */;
                        valueSchemaKey = this.chooseCommonArrayElementExpectedSchemaKey(descriptors);
                    }
                }
                else if (allScalars) {
                    const firstType = descriptors[0].typeCode;
                    const sameType = descriptors.every((d) => d.typeCode === firstType);
                    if (sameType && descriptors.every((d) => d.isPrimitive)) {
                        valueType = firstType;
                        valueSchemaKey = null;
                    }
                    else if (descriptors.every((d) => !d.isPrimitive && !d.isArray) &&
                        descriptors.every((d) => d.refSchemaKey === descriptors[0].refSchemaKey)) {
                        valueType = 8 /* FieldType.Object */;
                        valueSchemaKey = descriptors[0].refSchemaKey;
                    }
                    else {
                        valueType = 8 /* FieldType.Object */;
                        valueSchemaKey = this.ensureEmptyObjectSchema();
                    }
                }
                else {
                    valueType = 8 /* FieldType.Object */;
                    valueSchemaKey = this.ensureEmptyObjectSchema();
                }
            }
            const schemaKey = `map-${valueType}-${valueSchemaKey ?? ""}`;
            this.ensureMapSchema(schemaKey, valueType, valueSchemaKey);
            return { category: "complex", schemaKey };
        }
        finally {
            this.exitAnalyzingRef(value);
        }
    }
    analyzeArray(value) {
        this.enterAnalyzingRef(value, "Encode analysis cycle detected in array.");
        try {
            const descriptors = [];
            for (const item of value) {
                if (item === null || item === undefined)
                    continue;
                descriptors.push(this.analyzeSlotValue(item));
            }
            if (descriptors.length > 0) {
                const firstType = descriptors[0].typeCode;
                const sameType = descriptors.every((d) => d.typeCode === firstType);
                if (sameType && descriptors.every((d) => d.isPrimitive && !d.isArray)) {
                    return { category: "primitive", typeCode: firstType | 128 /* FieldType.ArrayFlag */ };
                }
            }
            const elemSchemaKey = this.chooseExpectedSchemaKeyForObjectSlot(descriptors);
            const schemaKey = `arr-${8 /* FieldType.Object */}-${elemSchemaKey ?? ""}`;
            this.ensureArraySchema(schemaKey, 8 /* FieldType.Object */, elemSchemaKey);
            return { category: "complex", schemaKey };
        }
        finally {
            this.exitAnalyzingRef(value);
        }
    }
    chooseExpectedSchemaKeyForObjectSlot(descriptors) {
        const generic = this.ensureEmptyObjectSchema();
        if (descriptors.length === 0)
            return generic;
        let candidate = null;
        for (const descriptor of descriptors) {
            if (descriptor.isPrimitive)
                return generic;
            const current = descriptor.schemaKey;
            if (current === null)
                return generic;
            if (candidate === null) {
                candidate = current;
            }
            else if (candidate !== current) {
                return generic;
            }
        }
        return candidate ?? generic;
    }
    chooseCommonArrayElementExpectedSchemaKey(descriptors) {
        const generic = this.ensureEmptyObjectSchema();
        let candidate = null;
        for (const descriptor of descriptors) {
            if (!descriptor.isArray)
                return generic;
            if (descriptor.isPrimitive)
                return generic;
            const current = descriptor.refSchemaKey;
            if (current === null)
                return generic;
            if (candidate === null) {
                candidate = current;
            }
            else if (candidate !== current) {
                return generic;
            }
        }
        return candidate ?? generic;
    }
    ensureEmptyObjectSchema() {
        if (this.emptyObjectSchemaKey !== null)
            return this.emptyObjectSchemaKey;
        const key = "obj-";
        this.ensureObjectSchema(key, []);
        this.emptyObjectSchemaKey = key;
        return key;
    }
    ensureObjectSchema(key, fields) {
        if (this.schemaIndexByKey.has(key))
            return;
        const schema = {
            key,
            kind: 1 /* SchemaKind.Object */,
            index: this.schemas.length,
            fields: fields.map((field) => ({ ...field })),
        };
        this.schemaIndexByKey.set(key, schema.index);
        this.schemas.push(schema);
    }
    ensureMapSchema(key, valueType, valueSchemaKey) {
        if (this.schemaIndexByKey.has(key))
            return;
        const schema = {
            key,
            kind: 2 /* SchemaKind.Map */,
            index: this.schemas.length,
            valueType,
            valueSchemaKey,
        };
        this.schemaIndexByKey.set(key, schema.index);
        this.schemas.push(schema);
    }
    ensureArraySchema(key, elemType, elemSchemaKey) {
        if (this.schemaIndexByKey.has(key))
            return;
        const schema = {
            key,
            kind: 3 /* SchemaKind.Array */,
            index: this.schemas.length,
            elemType,
            elemSchemaKey,
        };
        this.schemaIndexByKey.set(key, schema.index);
        this.schemas.push(schema);
    }
    requireSchema(key) {
        const index = this.schemaIndexByKey.get(key);
        if (index === undefined) {
            throw new Error(`Schema key not found: ${key}`);
        }
        return this.schemas[index];
    }
    writeRoot(root, value, writer) {
        if (value === null || value === undefined) {
            writer.pushByte(SOBJ_NULL_TAG);
            return;
        }
        if (root.category === "primitive") {
            writer.pushByte(root.typeCode);
            this.writePrimitivePayload(writer, root.typeCode, value);
            return;
        }
        const schema = this.requireSchema(root.schemaKey);
        this.writeSchemaPointer(writer, schema.index);
        switch (schema.kind) {
            case 2 /* SchemaKind.Map */:
                this.writeMapBody(writer, schema, value);
                return;
            case 3 /* SchemaKind.Array */:
                this.writeArraySchemaBody(writer, schema, value);
                return;
            case 1 /* SchemaKind.Object */:
                writer.pushByte(SOBJ_PRESENT_EXPECTED_SCHEMA);
                this.writeObjectBody(writer, schema, value);
                return;
        }
    }
    writeObjectBody(writer, schema, value) {
        if (!isPlainObject(value)) {
            throw new Error("Object schema body requires a plain object value.");
        }
        this.enterEncodingRef(value, "Encode cycle detected while writing object graph.");
        try {
            for (const field of schema.fields) {
                this.writeValueByType(writer, field.typeCode, field.refSchemaKey, value[field.name]);
            }
        }
        finally {
            this.exitEncodingRef(value);
        }
    }
    writeMapBody(writer, schema, value) {
        if (!(value instanceof Map)) {
            throw new Error("Map schema body requires a Map<string, value>.");
        }
        this.enterEncodingRef(value, "Encode cycle detected while writing map graph.");
        try {
            this.writeLUInt(writer, value.size);
            for (const [rawKey, rawValue] of value) {
                const key = String(rawKey);
                this.writeDStrPointer(writer, this.addString(key));
                this.writeValueByType(writer, schema.valueType, schema.valueSchemaKey, rawValue);
            }
        }
        finally {
            this.exitEncodingRef(value);
        }
    }
    writeArraySchemaBody(writer, schema, value) {
        if (schema.elemType === 8 /* FieldType.Object */) {
            this.writeObjectArrayPayload(writer, schema.elemSchemaKey, value);
            return;
        }
        this.writePrimitiveArrayPayload(writer, schema.elemType, value);
    }
    writeValueByType(writer, typeCode, refSchemaKey, value) {
        const valueBaseType = baseType(typeCode);
        if (isArrayType(typeCode)) {
            if (valueBaseType === 8 /* FieldType.Object */) {
                this.writeObjectArrayPayload(writer, refSchemaKey, value);
            }
            else {
                this.writePrimitiveArrayPayload(writer, valueBaseType, value);
            }
            return;
        }
        if (valueBaseType === 8 /* FieldType.Object */) {
            this.writeObjectSlot(writer, refSchemaKey, value);
            return;
        }
        this.writePrimitiveScalarPayload(writer, valueBaseType, value);
    }
    writeObjectSlot(writer, expectedSchemaKey, value) {
        if (value === null || value === undefined) {
            writer.pushByte(SOBJ_NULL_TAG);
            return;
        }
        if (expectedSchemaKey === null) {
            throw new Error("Object slot requires expected schema key.");
        }
        const analysis = this.analyzeValue(value);
        if (analysis.category === "primitive") {
            writer.pushByte(analysis.typeCode);
            this.writePrimitivePayload(writer, analysis.typeCode, value);
            return;
        }
        const actualSchema = this.requireSchema(analysis.schemaKey);
        if (actualSchema.kind === 3 /* SchemaKind.Array */) {
            writer.pushByte(8 /* FieldType.Object */ | 128 /* FieldType.ArrayFlag */);
            this.writeObjectArrayPayload(writer, expectedSchemaKey, value);
            return;
        }
        if (analysis.schemaKey === expectedSchemaKey) {
            writer.pushByte(SOBJ_PRESENT_EXPECTED_SCHEMA);
            this.writeSchemaBody(writer, actualSchema, value);
            return;
        }
        this.writeSchemaPointer(writer, actualSchema.index);
        this.writeSchemaBody(writer, actualSchema, value);
    }
    writeSchemaBody(writer, schema, value) {
        switch (schema.kind) {
            case 1 /* SchemaKind.Object */:
                this.writeObjectBody(writer, schema, value);
                return;
            case 2 /* SchemaKind.Map */:
                this.writeMapBody(writer, schema, value);
                return;
            case 3 /* SchemaKind.Array */:
                this.writeArraySchemaBody(writer, schema, value);
                return;
        }
    }
    writeObjectArrayPayload(writer, expectedSchemaKey, value) {
        if (value === null || value === undefined) {
            writer.pushByte(LUINT_NULL);
            return;
        }
        if (!Array.isArray(value)) {
            throw new Error("Object[] payload requires an array value.");
        }
        if (expectedSchemaKey === null) {
            throw new Error("Object[] payload requires expected element schema key.");
        }
        this.enterEncodingRef(value, "Encode cycle detected while writing array graph.");
        try {
            this.writeLUInt(writer, value.length);
            for (const item of value) {
                this.writeObjectSlot(writer, expectedSchemaKey, item);
            }
        }
        finally {
            this.exitEncodingRef(value);
        }
    }
    writePrimitivePayload(writer, typeCode, value) {
        if (isArrayType(typeCode)) {
            this.writePrimitiveArrayPayload(writer, baseType(typeCode), value);
        }
        else {
            this.writePrimitiveScalarPayload(writer, typeCode, value);
        }
    }
    writePrimitiveScalarPayload(writer, baseTypeCode, value) {
        switch (baseTypeCode) {
            case 1 /* FieldType.Integer */:
                this.writeLInt(writer, value === undefined ? null : value);
                return;
            case 2 /* FieldType.Float4Bytes */:
                this.writeFloat4(writer, value === undefined ? null : value);
                return;
            case 3 /* FieldType.Float8Bytes */:
                this.writeFloat8(writer, value === undefined ? null : value);
                return;
            case 4 /* FieldType.Boolean */:
                this.writeBool(writer, value === undefined ? null : value);
                return;
            case 5 /* FieldType.Date */:
                this.writeDDate(writer, value === undefined ? null : value);
                return;
            case 6 /* FieldType.String */:
                this.writeDStrOrNull(writer, value === undefined ? null : value);
                return;
            case 7 /* FieldType.Bytes */:
                this.writeBarr(writer, value === undefined ? null : value);
                return;
            default:
                throw new Error(`Unsupported primitive scalar type: ${baseTypeCode}`);
        }
    }
    writePrimitiveArrayPayload(writer, baseTypeCode, value) {
        if (value === null || value === undefined) {
            writer.pushByte(LUINT_NULL);
            return;
        }
        if (!Array.isArray(value)) {
            throw new Error("Primitive array payload requires an array value.");
        }
        this.enterEncodingRef(value, "Encode cycle detected while writing array graph.");
        try {
            this.writeLUInt(writer, value.length);
            for (const item of value) {
                this.writePrimitiveScalarPayload(writer, baseTypeCode, item);
            }
        }
        finally {
            this.exitEncodingRef(value);
        }
    }
    writeSchemaTable(writer) {
        this.writeLUInt(writer, this.schemas.length);
        for (const schema of this.schemas) {
            this.writeSchema(writer, schema);
        }
    }
    writeSchema(writer, schema) {
        switch (schema.kind) {
            case 1 /* SchemaKind.Object */:
                this.writeObjectSchema(writer, schema);
                return;
            case 2 /* SchemaKind.Map */:
                this.writeMapSchema(writer, schema);
                return;
            case 3 /* SchemaKind.Array */:
                this.writeArraySchema(writer, schema);
                return;
        }
    }
    writeObjectSchema(writer, schema) {
        const fieldCount = schema.fields.length;
        if (fieldCount <= SMAT_OBJ_LITERAL_MAX_FIELDS) {
            writer.pushByte(fieldCount);
        }
        else {
            writer.pushByte(SMAT_OBJ_LUINT_TAG);
            this.writeLUInt(writer, fieldCount);
        }
        for (const field of schema.fields) {
            writer.pushByte(field.typeCode);
            this.writeDStrPointer(writer, this.addString(field.name));
            if (baseType(field.typeCode) === 8 /* FieldType.Object */) {
                if (field.refSchemaKey === null) {
                    throw new Error(`Object field without ref schema key: ${field.name}`);
                }
                this.writeSchemaPointer(writer, this.requireSchema(field.refSchemaKey).index);
            }
        }
    }
    writeMapSchema(writer, schema) {
        const valueType = schema.valueType;
        const valueBaseType = baseType(valueType);
        if (valueBaseType < 1 /* FieldType.Integer */ || valueBaseType > 8 /* FieldType.Object */) {
            throw new Error(`Invalid map valueType for SMAT: ${valueType}`);
        }
        const ord = valueBaseType - 1;
        const smat = isArrayType(valueType) ? SMAT_MAP_ARR_BASE_TAG + ord : SMAT_MAP_BASE_TAG + ord;
        writer.pushByte(smat);
        if (valueBaseType === 8 /* FieldType.Object */) {
            if (schema.valueSchemaKey === null) {
                throw new Error(`Map Object/Object[] value without ref schema key: ${schema.key}`);
            }
            this.writeSchemaPointer(writer, this.requireSchema(schema.valueSchemaKey).index);
        }
    }
    writeArraySchema(writer, schema) {
        if (isArrayType(schema.elemType)) {
            throw new Error(`Array schema elemType cannot have ArrayFlag: ${schema.elemType}`);
        }
        const ord = schema.elemType - 1;
        if (ord < 0)
            throw new Error(`Invalid array elemType for SMAT: ${schema.elemType}`);
        writer.pushByte(SMAT_ARR_BASE_TAG + ord);
        if (schema.elemType === 8 /* FieldType.Object */) {
            if (schema.elemSchemaKey === null) {
                throw new Error(`Array Object element without ref schema key: ${schema.key}`);
            }
            this.writeSchemaPointer(writer, this.requireSchema(schema.elemSchemaKey).index);
        }
    }
    writeStringTable(writer) {
        this.writeLUInt(writer, this.strings.length);
        for (const value of this.strings) {
            this.writeLStr(writer, value);
        }
    }
    writeDateTable(writer) {
        this.writeLUInt(writer, this.dateRows.length);
        for (const ticks of this.dateRows) {
            writer.pushBytes(this.bigIntToBigEndianBytes(ticks, 8));
        }
    }
    addString(value) {
        const existing = this.stringIndexByValue.get(value);
        if (existing !== undefined)
            return existing;
        const index = this.strings.length;
        this.strings.push(value);
        this.stringIndexByValue.set(value, index);
        return index;
    }
    addDate(value) {
        const ticks = jsDateToTicks(value);
        const existingPointer = this.datePointerByTicks.get(ticks);
        if (existingPointer !== undefined) {
            return { mode: "pointer", ticks, index: existingPointer };
        }
        if (this.seenDateTicksOnce.has(ticks)) {
            const index = this.dateRows.length;
            this.dateRows.push(ticks);
            this.datePointerByTicks.set(ticks, index);
            return { mode: "pointer", ticks, index };
        }
        this.seenDateTicksOnce.add(ticks);
        return { mode: "inline", ticks, index: -1 };
    }
    writeLUInt(writer, value) {
        if (!Number.isInteger(value) || value < 0) {
            throw new Error(`Invalid LUINT value: ${value}`);
        }
        if (value <= LUINT_B0_MAX) {
            writer.pushByte(value);
            return;
        }
        if (value <= LUINT_B8_MAX) {
            writer.pushBytes([LUINT_B8, value - LUINT_B8_BASE_VALUE]);
            return;
        }
        const bigValue = BigInt(value);
        const byteCount = this.minByteCountUnsigned(bigValue, 2, 8);
        writer.pushByte(LUINT_16 + (byteCount - 2));
        writer.pushBytes(this.bigIntToBigEndianBytes(bigValue, byteCount));
    }
    writeLInt(writer, value) {
        if (value === null) {
            writer.pushByte(LINT_NULL_TAG);
            return;
        }
        const bigValue = typeof value === "bigint" ? value : BigInt(Math.trunc(value));
        if (bigValue >= 0n && bigValue <= BigInt(LINT_POS_LITERAL_MAX_VALUE)) {
            writer.pushByte(Number(bigValue));
            return;
        }
        if (bigValue >= -BigInt(LINT_NEG_LITERAL_COUNT) && bigValue <= -1n) {
            writer.pushByte(Number(BigInt(LINT_POS_LITERAL_MAX_VALUE) - bigValue));
            return;
        }
        if (bigValue >= BigInt(LINT_POS_PLUS_U8_BASE_VALUE) && bigValue <= BigInt(LINT_POS_PLUS_U8_MAX_VALUE)) {
            writer.pushBytes([LINT_POS_PLUS_U8_TAG, Number(bigValue - BigInt(LINT_POS_PLUS_U8_BASE_VALUE))]);
            return;
        }
        if (bigValue <= -BigInt(LINT_NEG_PLUS_U8_BASE_MAG) && bigValue >= -BigInt(LINT_NEG_PLUS_U8_MAX_MAG)) {
            writer.pushBytes([LINT_NEG_PLUS_U8_TAG, Number((-bigValue) - BigInt(LINT_NEG_PLUS_U8_BASE_MAG))]);
            return;
        }
        if (bigValue >= 0n) {
            const byteCount = this.minByteCountUnsigned(bigValue, 2, 8);
            writer.pushByte(LINT_POS_U16_TAG + (byteCount - 2));
            writer.pushBytes(this.bigIntToBigEndianBytes(bigValue, byteCount));
            return;
        }
        const magnitude = -bigValue;
        const byteCount = this.minByteCountUnsigned(magnitude, 2, 8);
        writer.pushByte(LINT_NEG_U16_TAG + (byteCount - 2));
        writer.pushBytes(this.bigIntToBigEndianBytes(magnitude, byteCount));
    }
    writeBool(writer, value) {
        if (value === null) {
            writer.pushByte(BOOL_NULL);
        }
        else {
            writer.pushByte(value ? BOOL_TRUE : BOOL_FALSE);
        }
    }
    writeFloat4(writer, value) {
        if (value === null) {
            writer.pushBytes([FLOAT_NULL_SENTINEL_B0, FLOAT_NULL_SENTINEL_B1]);
            return;
        }
        const buffer = new ArrayBuffer(4);
        const view = new DataView(buffer);
        view.setFloat32(0, value, false);
        const bytes = new Uint8Array(buffer);
        if (bytes[0] === FLOAT_NULL_SENTINEL_B0 && bytes[1] === FLOAT_NULL_SENTINEL_B1) {
            throw new Error("Non-null Float4 payload starts with the reserved null sentinel [255,255].");
        }
        writer.pushBytes(bytes);
    }
    writeFloat8(writer, value) {
        if (value === null) {
            writer.pushBytes([FLOAT_NULL_SENTINEL_B0, FLOAT_NULL_SENTINEL_B1]);
            return;
        }
        const buffer = new ArrayBuffer(8);
        const view = new DataView(buffer);
        view.setFloat64(0, value, false);
        const bytes = new Uint8Array(buffer);
        if (bytes[0] === FLOAT_NULL_SENTINEL_B0 && bytes[1] === FLOAT_NULL_SENTINEL_B1) {
            throw new Error("Non-null Float8 payload starts with the reserved null sentinel [255,255].");
        }
        writer.pushBytes(bytes);
    }
    writeLStr(writer, value) {
        if (value === null) {
            writer.pushByte(LUINT_NULL);
            return;
        }
        const bytes = UTF8_ENCODER.encode(value);
        this.writeLUInt(writer, bytes.length);
        writer.pushBytes(bytes);
    }
    writeDStrOrNull(writer, value) {
        if (value === null) {
            writer.pushByte(DSTR_NULL_TAG);
            return;
        }
        this.writeDStrPointer(writer, this.addString(value));
    }
    writeDStrPointer(writer, index) {
        if (index < 0)
            throw new Error(`Invalid DSTR pointer index: ${index}`);
        if (index < DSTR_PTR_LITERAL_COUNT) {
            writer.pushByte(DSTR_PTR_LITERAL_BASE_TAG + index);
            return;
        }
        if (index <= DSTR_PTR_LITERAL_COUNT + 255) {
            writer.pushBytes([DSTR_PTR_U8_TAG, index - DSTR_PTR_LITERAL_COUNT]);
            return;
        }
        if (index <= 0xffff) {
            writer.pushByte(DSTR_PTR_U16_TAG);
            writer.pushBytes([(index >> 8) & 0xff, index & 0xff]);
            return;
        }
        if (index <= 0xffffff) {
            writer.pushByte(DSTR_PTR_U24_TAG);
            writer.pushBytes([(index >> 16) & 0xff, (index >> 8) & 0xff, index & 0xff]);
            return;
        }
        writer.pushByte(DSTR_PTR_U32_TAG);
        writer.pushBytes([(index >>> 24) & 0xff, (index >>> 16) & 0xff, (index >>> 8) & 0xff, index & 0xff]);
    }
    writeDDate(writer, value) {
        if (value === null) {
            writer.pushByte(DDATE_NULL_TAG);
            return;
        }
        const encoded = this.addDate(value);
        if (encoded.mode === "inline") {
            writer.pushBytes(this.bigIntToBigEndianBytes(encoded.ticks, 8));
            return;
        }
        const index = encoded.index;
        if (index <= DDATE_PTR_LITERAL_COUNT - 1) {
            writer.pushByte(DDATE_PTR_LITERAL_BASE_TAG + index);
            return;
        }
        if (index <= DDATE_PTR_U8_MAX_INDEX) {
            writer.pushBytes([DDATE_PTR_U8_TAG, index - DDATE_PTR_U8_BASE_INDEX]);
            return;
        }
        if (index <= 0xffff) {
            writer.pushByte(DDATE_PTR_U16_TAG);
            writer.pushBytes([(index >> 8) & 0xff, index & 0xff]);
            return;
        }
        if (index <= 0xffffff) {
            writer.pushByte(DDATE_PTR_U24_TAG);
            writer.pushBytes([(index >> 16) & 0xff, (index >> 8) & 0xff, index & 0xff]);
            return;
        }
        writer.pushByte(DDATE_PTR_U32_TAG);
        writer.pushBytes([(index >>> 24) & 0xff, (index >>> 16) & 0xff, (index >>> 8) & 0xff, index & 0xff]);
    }
    writeBarr(writer, value) {
        if (value === null) {
            writer.pushByte(LUINT_NULL);
            return;
        }
        const bytes = toBytesInput(value);
        this.writeLUInt(writer, bytes.length);
        writer.pushBytes(bytes);
    }
    writeSchemaPointer(writer, index) {
        if (index < 0)
            throw new Error(`Schema index cannot be negative: ${index}`);
        if (index <= 0xff) {
            writer.pushBytes([SOBJ_SCHEMA_PTR_1B_TAG, index]);
            return;
        }
        if (index <= 0xffff) {
            writer.pushByte(SOBJ_SCHEMA_PTR_2B_TAG);
            writer.pushBytes([(index >> 8) & 0xff, index & 0xff]);
            return;
        }
        if (index <= 0xffffff) {
            writer.pushByte(SOBJ_SCHEMA_PTR_3B_TAG);
            writer.pushBytes([(index >> 16) & 0xff, (index >> 8) & 0xff, index & 0xff]);
            return;
        }
        throw new Error(`Schema index is too large (max 0xFFFFFF): ${index}`);
    }
    minByteCountUnsigned(value, min, max) {
        let byteCount = 1;
        let temp = value;
        while (temp > 0xffn) {
            byteCount += 1;
            temp >>= 8n;
        }
        if (byteCount < min)
            byteCount = min;
        if (byteCount > max) {
            throw new Error(`Unsigned value too large to encode in ${max} bytes.`);
        }
        return byteCount;
    }
    bigIntToBigEndianBytes(value, byteCount) {
        const out = new Uint8Array(byteCount);
        let temp = value;
        for (let i = byteCount - 1; i >= 0; i--) {
            out[i] = Number(temp & 0xffn);
            temp >>= 8n;
        }
        return out;
    }
    enterAnalyzingRef(value, message) {
        if (!this.needsRefTracking(value))
            return;
        if (this.analyzingRefs.has(value))
            throw new Error(message);
        this.analyzingRefs.add(value);
    }
    exitAnalyzingRef(value) {
        if (!this.needsRefTracking(value))
            return;
        this.analyzingRefs.delete(value);
    }
    enterEncodingRef(value, message) {
        if (!this.needsRefTracking(value))
            return;
        if (this.encodingRefs.has(value))
            throw new Error(message);
        this.encodingRefs.add(value);
    }
    exitEncodingRef(value) {
        if (!this.needsRefTracking(value))
            return;
        this.encodingRefs.delete(value);
    }
    needsRefTracking(value) {
        if (value === null || value === undefined)
            return false;
        if (typeof value !== "object")
            return false;
        if (value instanceof Date)
            return false;
        if (isByteLike(value))
            return false;
        return true;
    }
}
export async function decode(source) {
    let bytes = toUint8Array(source);
    if (bytes.length === 0) {
        return null;
    }
    // 1) GZIP?
    if (isGZip(bytes)) {
        bytes = await gunzip(bytes);
        if (bytes.length === 0) {
            return null;
        }
    }
    // 2) JSON?
    if (isJson(bytes)) {
        const jsonText = UTF8_DECODER.decode(bytes);
        return JSON.parse(jsonText);
    }
    // 3) BYTERY?
    if (isBytery(bytes)) {
        return new Decoder(bytes).decode();
    }
    // 4) neither JSON nor BYTERY
    throw new Error("Source bytes aren't Bytery, JSON, or supported GZIP-wrapped content.");
}
export function encode(value) {
    return new Encoder().encode(value);
}
const Bytery = Object.freeze({ decode, encode });
export default Bytery;

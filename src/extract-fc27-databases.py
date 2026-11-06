#!/usr/bin/env python3
"""Extract fifa_ng_db files from a local EA SPORTS FC 27 CAS archive."""
import argparse
import ctypes
import os
import string
import struct
import zlib

DB_SIGNATURE = bytes((0x44, 0x42, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00))
DECOMPRESS = None


def find_game_directory():
    env = os.environ.get('FC27_GAME_DIR')
    if env and os.path.isdir(env):
        return env
    candidates = []
    for drive in string.ascii_uppercase:
        candidates.extend((
            f'{drive}:\\Program Files (x86)\\Steam\\steamapps\\common\\EA SPORTS FC 27',
            f'{drive}:\\SteamLibrary\\steamapps\\common\\EA SPORTS FC 27',
            f'{drive}:\\Program Files\\EA Games\\EA SPORTS FC 27',
        ))
    for candidate in candidates:
        if os.path.isdir(candidate):
            return candidate
    raise RuntimeError('No encontré FC27. Instálalo o pasa --game-dir con la carpeta del juego.')


def load_oodle(game_dir):
    global DECOMPRESS
    dll = os.path.join(game_dir, 'oo2core_9_win64.dll')
    if not os.path.isfile(dll):
        raise RuntimeError(f'No encontré la biblioteca Oodle del juego: {dll}')
    library = ctypes.WinDLL(dll)
    DECOMPRESS = library.OodleLZ_Decompress
    DECOMPRESS.restype = ctypes.c_longlong
    DECOMPRESS.argtypes = [ctypes.c_void_p, ctypes.c_longlong, ctypes.c_void_p,
                           ctypes.c_longlong, ctypes.c_int, ctypes.c_int,
                           ctypes.c_int, ctypes.c_void_p, ctypes.c_longlong,
                           ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p,
                           ctypes.c_longlong, ctypes.c_int]


def decompress_oodle(payload, expected_size):
    source = ctypes.create_string_buffer(payload)
    output = ctypes.create_string_buffer(expected_size)
    size = DECOMPRESS(source, len(payload), output, expected_size,
                      1, 0, 0, None, 0, None, None, None, 0, 3)
    if size != expected_size:
        raise ValueError('Oodle devolvió un tamaño inesperado')
    return output.raw[:expected_size]


def decode_block(file, offset, file_size):
    if offset + 8 > file_size:
        return None
    file.seek(offset)
    header = file.read(8)
    if len(header) != 8:
        return None
    unpacked = struct.unpack_from('>I', header, 0)[0]
    code = struct.unpack_from('>H', header, 4)[0]
    compressed = ((code & 0x0F) << 16) | struct.unpack_from('>H', header, 6)[0]
    kind = (code >> 8) & 0xFF
    if not unpacked or unpacked > 0x40000 or not compressed or compressed > 0x80000:
        return None
    payload = file.read(compressed)
    if len(payload) != compressed:
        return None
    try:
        if kind in (0x19, 0x0F, 0x11, 0x15):
            data = decompress_oodle(payload, unpacked)
        elif kind == 0x00:
            data = payload[:unpacked]
        elif kind == 0x02:
            data = zlib.decompress(payload)
        else:
            return None
        if len(data) != unpacked:
            return None
        return data, offset + 8 + compressed
    except Exception:
        return None


def plausible_database(data, offset):
    if offset + 24 > len(data):
        return None
    size = struct.unpack_from('<I', data, offset + 8)[0]
    tables = struct.unpack_from('<I', data, offset + 16)[0]
    if 20 <= tables <= 5000 and 1000 <= size <= 400_000_000:
        return size
    return None


def extract_cas(cas_path, label, output_dir):
    file_size = os.path.getsize(cas_path)
    extracted = []
    with open(cas_path, 'rb') as file:
        offset = 0
        carry = b''
        carry_base = 0
        stream_position = 0
        capture = None
        misaligned = 0
        while offset + 8 <= file_size:
            result = decode_block(file, offset, file_size)
            if result is None:
                misaligned += 1
                offset += 1
                if misaligned > 5_000_000:
                    break
                continue
            block, next_offset = result
            misaligned = 0
            if capture:
                remaining = capture['size'] - len(capture['data'])
                capture['data'].extend(block[:remaining])
                if len(capture['data']) >= capture['size']:
                    path = os.path.join(output_dir, f'{label}_{capture["stream"]}.fifa_ng_db')
                    with open(path, 'wb') as output:
                        output.write(capture['data'])
                    extracted.append(path)
                    capture = None
            else:
                combined = carry + block
                search_from = 0
                while True:
                    index = combined.find(DB_SIGNATURE, search_from)
                    if index < 0:
                        break
                    db_size = plausible_database(combined, index)
                    if db_size:
                        data = bytearray(combined[index:])
                        stream = carry_base + index
                        if len(data) >= db_size:
                            path = os.path.join(output_dir, f'{label}_{stream}.fifa_ng_db')
                            with open(path, 'wb') as output:
                                output.write(data[:db_size])
                            extracted.append(path)
                        else:
                            capture = {'size': db_size, 'data': data, 'stream': stream}
                        break
                    search_from = index + 1
            stream_position += len(block)
            carry = block[-64:]
            carry_base = stream_position - len(carry)
            offset = next_offset
    return extracted


def main():
    parser = argparse.ArgumentParser(description='Extrae bases de datos locales de FC27.')
    parser.add_argument('--game-dir', default=None, help='carpeta de instalación de FC27')
    parser.add_argument('--cas', required=True, help='archivo CAS, por ejemplo cas_08.cas')
    parser.add_argument('--out', required=True, help='carpeta donde guardar las bases extraídas')
    args = parser.parse_args()
    game_dir = args.game_dir or find_game_directory()
    bundle = os.path.join(game_dir, 'Data', 'Win32', 'superbundlelayout', 'installpackage_00')
    cas_path = os.path.join(bundle, args.cas)
    if not os.path.isfile(cas_path):
        raise RuntimeError(f'No encontré el archivo del juego: {cas_path}')
    os.makedirs(args.out, exist_ok=True)
    load_oodle(game_dir)
    found = extract_cas(cas_path, args.cas.removesuffix('.cas'), args.out)
    print(f'{args.cas}: extraídas {len(found)} bases de datos.')


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        raise SystemExit(str(error))

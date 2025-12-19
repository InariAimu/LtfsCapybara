<div align="center">

<img width="200" src="logo.png">

# LtfsCapybara

[![capybara](https://img.shields.io/badge/Ltfs-Capybara-brown)](#)
[![License](https://img.shields.io/static/v1?label=LICENSE&message=GNU%20GPLv3&color=lightrey)](./blob/main/LICENSE)

</div>

LtfsCapybara is a LTFS implementation designed for managing and accessing data stored on LTO tape drives.

## Features

- LTFS 2.4 standard compliance.
- Format, write, read, and verify LTFS tapes.
- LTO-5, LTO-6 tapes tested on HP LTO Ultrium 6250 drive.
- Zero copy pipeline for high performance data transfer, optimized for samba transfer. 160 MB/s continuous write speed tested on LTO-6 tape drive with 10GbE network.
- Multi-threaded read/write operations.

## Instrction

Please use `Test` project for implementation reference.

## Disclaimer

This project is under active development and may have deprecated or unstable features. Use at your own risk.
This is NOT a backup solution. Please ensure you have proper backups of your data before using this software.

> This software is provided "as is", without warranty of any kind, express or implied, including but not limited to the warranties of merchantability, fitness for a particular purpose and noninfringement. In no event shall the authors or copyright holders be liable for any claim, damages or other liability, whether in an action of contract, tort or otherwise, arising from, out of or in connection with the software or the use or other dealings in the software.

## License

Licensed in GNU GPLv3 with ❤.

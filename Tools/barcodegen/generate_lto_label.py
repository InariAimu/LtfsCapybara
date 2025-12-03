from PIL import Image, ImageDraw, ImageFont
import barcode
from barcode.writer import ImageWriter
import re
from io import BytesIO
import os,sys

def mm_to_px(mm, dpi=300):
    return int(round(mm/25.4*dpi*0.95))

def generate_lto_label(
    code,
    out_png="label.png",
    dpi=300,
    label_width_mm=78,
    label_height_mm=16,
    corner_radius_mm=1.5,
    border_thickness_mm=0.1,
    left_right_margin_mm=3.5,
    digit_colors=None,
    default_bg=(255,255,255)
):
    """
    Generate an LTO tape barcode label
    Features:
    - The last two characters are placed in the same cell
    - Adds left and right margins
    """

    # Default color map (digits 0-9)
    if digit_colors is None:
        digit_colors = {
            "0": (200,0,0),
            "1": (255,220,50),
            "2": (144,238,144),
            "3": (173,216,230),
            "4": (211,211,211),
            "5": (255,165,0),
            "6": (255,192,203),
            "7": (0,200,0),
            "8": (204,153,0),
            "9": (160,130,160),
        }
    
    # 0: red, 1: yellow, 2: light green, 3: light blue,
    # 4: light gray, 5: orange, 6: pink, 7: green,
    # 8: brownish, 9: purplish-gray

    # Validate barcode
    if not re.fullmatch(r"[A-Za-z0-9]+", code):
        raise ValueError("Barcode may only contain letters and digits")
    if len(code) < 6 or len(code) > 8:
        raise ValueError("Barcode length must be 6-8 characters (e.g. CA0001L6)")

    # Dimensions
    W = mm_to_px(label_width_mm, dpi)
    
    H = mm_to_px(label_height_mm, dpi)
    margin_px = mm_to_px(left_right_margin_mm, dpi)
    
    W_core = W - 2 * margin_px
    corner_r = mm_to_px(corner_radius_mm, dpi)
    border_t = max(1, mm_to_px(border_thickness_mm, dpi))

    # Create canvas
    img = Image.new("RGB", (W, H), "white")
    draw = ImageDraw.Draw(img)

    # Rounded rectangle border
    draw.rounded_rectangle([0,0,W-1,H-1], radius=corner_r, outline="black", width=border_t)

    # Divide top (text) and bottom (barcode) areas
    text_area_h = int(H * 0.3)
    barcode_area_h = H - text_area_h

    # Number of front cells + merged cell for the last two characters
    n_cells = len(code) - 1
    cell_w = W_core // n_cells

    # Fonts
    try:
        font_main = ImageFont.truetype("arialbd.ttf", size=int(text_area_h*0.9))
        font_small = ImageFont.truetype("arialbd.ttf", size=int(text_area_h*0.7))
    except:
        font_main = ImageFont.load_default()
        font_small = ImageFont.load_default()

    # Draw the first n-2 characters
    for i, ch in enumerate(code[:-2]):
        bg = digit_colors.get(ch, default_bg)
        x0 = margin_px + i*cell_w
        y0, x1, y1 = 0, x0+cell_w, text_area_h
        draw.rectangle([x0,y0,x1,y1], fill=bg, outline="black")

        l,t,r,b = draw.textbbox((0,0), ch, font=font_main)
        w, h = r - l, b - t
        
        tx = x0 + (cell_w - w)//2
        ty = y0 + (text_area_h - h)//2 - 6
        draw.text((tx,ty), ch, font=font_main, fill="black")

    # Draw the last cell (contains two characters)
    last_ch = code[-2:]
    idx = n_cells - 1
    x0 = margin_px + idx*cell_w
    y0, x1, y1 = 0, x0+cell_w, text_area_h
    draw.rectangle([x0,y0,x1,y1], fill=default_bg, outline="black")

    l,t,r,b = draw.textbbox((0,0), last_ch, font=font_small)
    w, h = r - l, b - t
    
    tx = x0 + (cell_w - w)//2
    ty = y0 + (text_area_h - h)//2 - 5
    draw.text((tx,ty), last_ch, font=font_small, fill="black")

    # Generate Code39 barcode
    code_class = barcode.get_barcode_class("code39")
    my_code = code_class(code, writer=ImageWriter(), add_checksum=False)

    bio = BytesIO()
    my_code.write(bio, {
        "module_height": barcode_area_h,
        "module_width": 0.2,
        "quiet_zone": 1.0,
        "font_size": 0,
        "dpi": dpi
    })
    bio.seek(0)
    barcode_img = Image.open(bio).convert("RGB")

    # Adjust width
    barcode_img = barcode_img.resize((W_core, barcode_area_h), Image.LANCZOS)

    # Paste barcode (considering left/right margins)
    img.paste(barcode_img, (margin_px, text_area_h + 1))

    # Save
    img.save(out_png, dpi=(dpi,dpi))
    print(f"Generated {out_png} ({W}x{H}px, {dpi} DPI)")

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python generate_lto_label.py <BARCODE> [out.png]")
        sys.exit(1)
    code = sys.argv[1].strip()
    out = sys.argv[2].strip() if len(sys.argv) >= 3 else f"{code}_label.png"
    try:
        generate_lto_label(code, out)
    except Exception as e:
        print("Generation failed:", e)
        raise
    
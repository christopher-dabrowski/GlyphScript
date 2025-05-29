// Class method accessing and modifying multiple fields
🎒 Rectangle 📖
    🔢 width
    🔢 height

    ⚙️ 🔢 getArea() 📖
        ↩️ width * height
    📕

    ⚙️ 🔢 getPerimeter() 📖
        ↩️ 2 * (width + height)
    📕

    ⚙️ 🕳️ scale(🔢 factor) 📖
        width = width * factor
        height = height * factor
    📕

    ⚙️ 🆗 isSquare() 📖
        ↩️ width ⚖️ height
    📕
📕

🎒 Rectangle rect
rect.width = 10
rect.height = 20

📢 rect.getArea()
📢 rect.getPerimeter()
📢 rect.isSquare()

rect.scale(2)
📢 rect.width
📢 rect.height
📢 rect.getArea()

rect.width = 40
📢 rect.isSquare()

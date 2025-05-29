// Complex class interaction with method chaining
🎒 Counter 📖
    🔢 value
    
    ⚙️ 🔢 increment() 📖
        value = value + 1
        ↩️ value
    📕
    
    ⚙️ 🔢 decrement() 📖
        value = value - 1
        ↩️ value
    📕
    
    ⚙️ 🔢 add(🔢 amount) 📖
        value = value + amount
        ↩️ value
    📕
    
    ⚙️ 🔢 getValue() 📖
        ↩️ value
    📕
    
    ⚙️ 🕳️ reset() 📖
        value = 0
    📕
📕

🎒 Counter counter
counter.value = 5

📢 counter.getValue()
📢 counter.increment()
📢 counter.increment()
📢 counter.add(10)
📢 counter.decrement()
📢 counter.getValue()

counter.reset()
📢 counter.getValue()

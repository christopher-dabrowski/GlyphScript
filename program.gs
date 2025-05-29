// Basic class system test - Person class with fields and methods
🎒 Person 📖
    🔢 age
    🔤 name

    ⚙️ 🔤 getName() 📖
        ↩️ name
    📕
    ⚙️ 🔢 getAge() 📖
        ↩️ age
    📕
    ⚙️ 🕳️ setAge(🔢 newAge) 📖
        age = newAge
    📕
📕

// Create a person instance
🎒 Person person
person.name = "Alice"
person.age = 25

// Test field access
📢 person.name
📢 person.age

// Test method calls
📢 person.getName()
📢 person.getAge()

// Test field assignment through method
person.setAge(30)
📢 person.age

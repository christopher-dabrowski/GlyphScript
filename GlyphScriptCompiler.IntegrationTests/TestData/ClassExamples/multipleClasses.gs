// Multiple classes interacting with each other
🎒 Student 📖
    🔤 name
    🔢 age
    🔢 grade
    
    ⚙️ 🔤 getName() 📖
        ↩️ name
    📕
    
    ⚙️ 🔢 getGrade() 📖
        ↩️ grade
    📕
    
    ⚙️ 🕳️ setGrade(🔢 newGrade) 📖
        grade = newGrade
    📕
📕

🎒 Teacher 📖
    🔤 subject
    🔢 experience
    
    ⚙️ 🔤 getSubject() 📖
        ↩️ subject
    📕
    
    ⚙️ 🔢 getExperience() 📖
        ↩️ experience
    📕
📕

🎒 Student alice
alice.name = "Alice"
alice.age = 20
alice.setGrade(95)

🎒 Teacher prof
prof.subject = "Mathematics"
prof.experience = 15

📢 alice.getName()
📢 alice.getGrade()
📢 prof.getSubject()
📢 prof.getExperience()

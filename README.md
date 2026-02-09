# AR Social Practice Companion (Demo)

A desktop/mobile AR application that lets users practice real-world social interactions with a realistic AI companion using voice.

Check it out on devpost:
https://devpost.com/software/parsar

---

## Problem

For people with social anxiety or neurodivergence, social interaction can be stressful and hard to practice safely.

Existing methods like role-play, scripted videos, or traditional chatbots often:

- Feel artificial or disconnected from real life  
- Lack emotional realism  
- Do not allow practice in real-world contexts  

Yet, exposure and rehearsal are proven methods for building confidence and social competence.

---

## Solution

This demo app uses augmented reality and conversational AI to create a safe, realistic environment where users can rehearse social interactions without real-world consequences.

**Current Features:**

- Place an AI companion in your environment (AR or desktop visualization)  
- Speak naturally to the AI by **pressing the spacebar** on your laptop  
- AI responds with realistic speech using **ElevenLabs**  
- Conversation logic and understanding powered by **Google Cloud API**  

The goal is to make social practice feel human, contextual, and repeatable.

---

## Demo Scope

This repository demonstrates a **single, controlled interaction flow** to showcase the core concept:

1. User places the AI companion in the scene  
2. User presses **spacebar** to talk  
3. AI responds naturally using text-to-speech  
4. Feedback is generated after the interaction  

> The demo focuses on **clarity, polish, and emotional realism** over breadth or full production features.

---

## Tech Stack

- **Unity (Desktop/Mobile AR)**  
- **AR Foundation**  
- **ElevenLabs API** – speech-to-text & text-to-speech  
- **Google Cloud API** – conversational logic and understanding  

---

## Architecture Overview

1. Voice input captured when the **spacebar is pressed**  
2. Speech is transcribed  
3. Transcription sent to Google Cloud API to generate AI response  
4. Response converted to natural speech with ElevenLabs and played through the AI companion  

---

## Why AR / Desktop Interaction?

Unlike traditional chat interfaces, this setup allows users to:

- Practice in a realistic or contextual environment  
- Maintain spatial awareness and eye-line (if using AR)  
- Rehearse social interactions safely on a laptop  

This makes practice more transferable to real-life situations.

---

## Future Work

- Multiple social scenarios (interviews, small talk, presentations)  
- Personalized difficulty and feedback based on user progress  
- Real-time emotion and tone analysis  
- Full session summaries and highlight reels  
- Accessibility features for neurodivergent users  

---

## Hackathon Note

This project was built during a hackathon and demonstrates a strong **core concept and interaction flow** rather than full production readiness. The demo prioritizes **user experience, clarity, and realism** over feature breadth.

---

## Team

Built by a team of students exploring the intersection of **augmented reality, AI, and mental health**.

# 📹 Scanner Vidéo IA Pro (v1.3.0)

**Scanner Vidéo IA Pro** est un logiciel professionnel développé en C# WPF, conçu pour l'analyse, le tri automatique et la recherche intelligente d'événements dans des flux ou fichiers vidéo.

Grâce à l'intégration de modèles de Deep Learning exécutés via **ONNX Runtime** (YOLOv8 + ANPR), l'application permet de détecter des personnes, véhicules (voitures, motos, camions, bus) et d'extraire automatiquement leurs plaques d'immatriculation avec accélération matérielle adaptative.

---

## 🚀 Téléchargement

📥 **[Télécharger l'installateur de Scanner Vidéo IA Pro (OneDrive)](https://1drv.ms/u/c/c142bf9687cc924c/IQAPY34xaLy0S5LgbABAA0ZmAdRN2jdUBgGii9HwanoHOf4?e=lYWRHi)**

---

## ✨ Fonctionnalités Principales

* ⚡ **Accélération Matérielle Adaptative (Cascade NPU > GPU > CPU) :**
  - Sélection dynamique ou manuelle du composant d'exécution (NPU Intel/AMD/Snapdragon via DirectML, GPU NVIDIA CUDA/DirectML, ou CPU multi-cœurs).
* 🚗 **Multi-Détection & Reconnaissance de Plaques (ANPR) :**
  - Détection en temps réel des personnes, voitures, motos, camions et bus avec YOLOv8.
  - Détection automatique et isolation des plaques d'immatriculation avec `anpr_yolov5s` + OCR Tesseract.
* 📊 **Rapports & Expatriation de Données :**
  - Exportation directe au format CSV / Excel avec horodatage, repère temporel et immatriculations.
  - Sauvegarde des images preuves avec incrustation des bounding boxes.
* 🖥️ **Interface Moderne WPF :**
  - Mode sombre / clair dynamique.
  - Inversion de disposition de la grille vidéo/tableau.
  - Filtrage interactif par type d'événement ou par numéro de plaque.
* 🔒 **Gestion des Licences & Mises à jour :**
  - Vérification en ligne de l'état de la licence (version d'essai, abonnement, clé d'activation).
  - Détection automatique des mises à jour logicielles via API Cloud.

---

## 🛠️ Spécifications Techniques

* **Framework :** .NET Framework 4.8 / WPF
* **Bibliothèques principales :**
  * `Microsoft.ML.OnnxRuntime.DirectML` / `Microsoft.ML.OnnxRuntime`
  * `OpenCvSharp4` & `OpenCvSharp4.WpfExtensions`
  * `Tesseract` (OCR Engine)
  * `CsvHelper`
* **Modèles Embarqués :**
  * `yolov8s.onnx` (Inférence objet)
  * `anpr_yolov5s.onnx` (Inférence plaques d'immatriculation)

---

## 💻 Configuration Requise

* **Système d'exploitation :** windows 8.1/Windows 10 / Windows 11 (64 bits)
* **Processeur :** CPU x64 avec support AVX2
* **Processeur Graphique / NPU (Recommandé) :** 
  * Carte graphique NVIDIA (avec pilotes CUDA à jour) ou GPU compatible DirectX 12.
  * NPU compatible DirectML (Intel Core Ultra, AMD Ryzen AI).

---

## 📦 Installation

1. Téléchargez l'installateur via le lien OneDrive ci-dessus.
2. Exécutez `Installation_ScannerIA_V1.3.exe` (Droits d'administration requis).
3. Suivez les étapes de l'assistant d'installation.
4. LANCEZ l'application depuis le raccourci du bureau.

---

*Développé par Elliott Magnier.*

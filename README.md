# Real Time Visualization Methods for Airborne Surveillance

## Overview:
This project implements a virtual reality (VR)-based system for airborne surveillance operators.
The system fuses data from multiple sources like the Automatic Identification System (AIS), radar and Electro-Optical/Infrared (EO/IR) camera
and presents real-time augmented reality (AR) overlays with uncertainty visualization directly in the Meta Quest 2 VR environment.

## Problem Statement
Operators on board airborne surveillance missions face challenges related to:
* Information overload from multiple sensor sources
* Uncertainty in sensor data is not communicated to the operator
* Cognitive load of manually correcting sensor detections
* Limited situational awareness with traditional 2D displays

## Solution
A VR-based interface with AR overlays that accomplishes several tasks. The system fuses AIS data with radar data using Kalman filtering.
Moreover, uncertainty in those sensors is visualized using confidence indicators. The system allows the operator to take control of the
EO/IR camera to identify unknown ships. Ultimately, all the necessary information is presented to the operator through AR overlays in 3D.

## Features
- Multi-Sensor Fusion
  * AIS implementation
  * Radar contact detection and identification
  * Kalman filter-based sensor fusion
  * Real-time position estimation with uncertainty quantification
- EO/IR Camera System
  * Virtual camera mounted underneath the aircraft with gimbal control
  * Pan, tilt and zoom controls in the VR controllers
  * Visual ship confirmation workflow
  * Adaptive SphereCast detection
- VR Interface with AR Overlays
  * 3D AR overlays on top of ships in the VR scene
  * Uncertainty-aware visualizations (confidence indicators, position uncertainty)
  * Deterministic AR overlays (when all the ships transmit AIS data and position is known)
  * Real-time distance, bearing and identity information
- Meta Quest 2 Integration
  * Full 6 degrees of freedom tracking
  * Intuitive controller-based interaction
  * Operator console with interactive display
  * Camera feed display with telemetry information
  * 

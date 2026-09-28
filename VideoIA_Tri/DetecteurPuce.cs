using System;
using Microsoft.ML.OnnxRuntime;

namespace VideoIA_Tri
{
    public enum ModeAccurate
    {
        Auto,
        NPU,
        GPU,
        CPU
    }

    public static class DetecteurPuce
    {
        public static SessionOptions ObtenirOptionsExecution(ModeAccurate modeSelectionne, out string descriptionPuce)
        {
            var options = new SessionOptions();

            switch (modeSelectionne)
            {
                case ModeAccurate.NPU:
                    try
                    {
                        options.AppendExecutionProvider_DML(1); // Periphérique NPU/VPU secondaire
                        descriptionPuce = "NPU (DirectML Device 1)";
                        return options;
                    }
                    catch
                    {
                        descriptionPuce = "NPU indisponible, bascule sur CPU";
                        break;
                    }

                case ModeAccurate.GPU:
                    try
                    {
                        options.AppendExecutionProvider_CUDA(0);
                        descriptionPuce = "GPU (NVIDIA CUDA)";
                        return options;
                    }
                    catch
                    {
                        try
                        {
                            options.AppendExecutionProvider_DML(0);
                            descriptionPuce = "GPU (DirectML Device 0)";
                            return options;
                        }
                        catch
                        {
                            descriptionPuce = "GPU indisponible, bascule sur CPU";
                            break;
                        }
                    }

                case ModeAccurate.CPU:
                    options.ExecutionMode = ExecutionMode.ORT_PARALLEL;
                    options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                    descriptionPuce = "CPU / iGPU Multi-coeurs";
                    return options;

                case ModeAccurate.Auto:
                default:
                    // Cascade : NPU -> GPU CUDA -> GPU DirectML -> CPU
                    try
                    {
                        options.AppendExecutionProvider_DML(1);
                        descriptionPuce = "Automatique : NPU";
                        return options;
                    }
                    catch { }

                    try
                    {
                        options.AppendExecutionProvider_CUDA(0);
                        descriptionPuce = "Automatique : GPU CUDA";
                        return options;
                    }
                    catch { }

                    try
                    {
                        options.AppendExecutionProvider_DML(0);
                        descriptionPuce = "Automatique : GPU DirectML";
                        return options;
                    }
                    catch { }

                    options.ExecutionMode = ExecutionMode.ORT_PARALLEL;
                    options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                    descriptionPuce = "Automatique : CPU";
                    return options;
            }

            options.ExecutionMode = ExecutionMode.ORT_PARALLEL;
            options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            return options;
        }
    }
}
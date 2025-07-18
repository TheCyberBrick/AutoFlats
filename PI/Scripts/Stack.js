﻿function createProcess() {
    var P = new ImageIntegration;
    P.images = [ // enabled, path, drizzlePath, localNormalizationDataPath
    ];
    P.inputHints = "fits-keywords normalize raw cfa signed-is-physical";
    P.combination = ImageIntegration.prototype.Average;
    P.weightMode = ImageIntegration.prototype.PSFSignalWeight;
    P.weightKeyword = "";
    P.csvWeightsFilePath = "";
    P.weightScale = ImageIntegration.prototype.WeightScale_BWMV;
    P.minWeight = 0.005000;
    P.csvWeights = "";
    P.adaptiveGridSize = 16;
    P.adaptiveNoScale = false;
    P.ignoreNoiseKeywords = false;
    P.normalization = ImageIntegration.prototype.AdditiveWithScaling;
    P.rejection = ImageIntegration.prototype.NoRejection;
    P.rejectionNormalization = ImageIntegration.prototype.Scale;
    P.minMaxLow = 1;
    P.minMaxHigh = 1;
    P.pcClipLow = 0.200;
    P.pcClipHigh = 0.100;
    P.sigmaLow = 4.000;
    P.sigmaHigh = 3.000;
    P.winsorizationCutoff = 5.000;
    P.linearFitLow = 5.000;
    P.linearFitHigh = 4.000;
    P.esdOutliersFraction = 0.30;
    P.esdAlpha = 0.05;
    P.esdLowRelaxation = 1.00;
    P.rcrLimit = 0.10;
    P.ccdGain = 1.00;
    P.ccdReadNoise = 10.00;
    P.ccdScaleNoise = 0.00;
    P.clipLow = true;
    P.clipHigh = true;
    P.rangeClipLow = true;
    P.rangeLow = 0.000000;
    P.rangeClipHigh = false;
    P.rangeHigh = 0.980000;
    P.mapRangeRejection = true;
    P.reportRangeRejection = false;
    P.largeScaleClipLow = false;
    P.largeScaleClipLowProtectedLayers = 2;
    P.largeScaleClipLowGrowth = 2;
    P.largeScaleClipHigh = false;
    P.largeScaleClipHighProtectedLayers = 2;
    P.largeScaleClipHighGrowth = 2;
    P.generate64BitResult = false;
    P.generateRejectionMaps = true;
    P.generateSlopeMaps = false;
    P.generateIntegratedImage = true;
    P.generateDrizzleData = false;
    P.closePreviousImages = false;
    P.bufferSizeMB = 16;
    P.stackSizeMB = 1024;
    P.autoMemorySize = true;
    P.autoMemoryLimit = 0.75;
    P.useROI = false;
    P.roiX0 = 0;
    P.roiY0 = 0;
    P.roiX1 = 0;
    P.roiY1 = 0;
    P.useCache = true;
    P.evaluateSNR = true;
    P.noiseEvaluationAlgorithm = ImageIntegration.prototype.NoiseEvaluation_MRS;
    P.mrsMinDataFraction = 0.010;
    P.psfStructureLayers = 5;
    P.psfType = ImageIntegration.prototype.PSFType_Moffat4;
    P.subtractPedestals = false;
    P.truncateOnOutOfRange = false;
    P.noGUIMessages = true;
    P.showImages = true;
    P.useFileThreads = true;
    P.fileThreadOverload = 1.00;
    P.useBufferThreads = true;
    P.maxBufferThreads = 0;
    /*
     * Read-only properties
     *
    P.integrationImageId = "";
    P.lowRejectionMapImageId = "";
    P.highRejectionMapImageId = "";
    P.slopeMapImageId = "";
    P.numberOfChannels = 0;
    P.numberOfPixels = 0;
    P.totalPixels = 0;
    P.outputRangeLow = 0;
    P.outputRangeHigh = 0;
    P.totalRejectedLowRK = 0;
    P.totalRejectedLowG = 0;
    P.totalRejectedLowB = 0;
    P.totalRejectedHighRK = 0;
    P.totalRejectedHighG = 0;
    P.totalRejectedHighB = 0;
    P.finalNoiseEstimateRK = 0.000e+00;
    P.finalNoiseEstimateG = 0.000e+00;
    P.finalNoiseEstimateB = 0.000e+00;
    P.finalNoiseScaleEstimateLowRK = 0.000000e+00;
    P.finalNoiseScaleEstimateLowG = 0.000000e+00;
    P.finalNoiseScaleEstimateLowB = 0.000000e+00;
    P.finalNoiseScaleEstimateHighRK = 0.000000e+00;
    P.finalNoiseScaleEstimateHighG = 0.000000e+00;
    P.finalNoiseScaleEstimateHighB = 0.000000e+00;
    P.finalNoiseAlgorithmRK = "";
    P.finalNoiseAlgorithmG = "";
    P.finalNoiseAlgorithmB = "";
    P.finalScaleEstimateRK = 0.0000e+00;
    P.finalScaleEstimateG = 0.0000e+00;
    P.finalScaleEstimateB = 0.0000e+00;
    P.finalLocationEstimateRK = 0.0000e+00;
    P.finalLocationEstimateG = 0.0000e+00;
    P.finalLocationEstimateB = 0.0000e+00;
    P.finalPSFTotalFluxEstimateRK = 0.0000e+00;
    P.finalPSFTotalFluxEstimateG = 0.0000e+00;
    P.finalPSFTotalFluxEstimateB = 0.0000e+00;
    P.finalPSFTotalPowerFluxEstimateRK = 0.0000e+00;
    P.finalPSFTotalPowerFluxEstimateG = 0.0000e+00;
    P.finalPSFTotalPowerFluxEstimateB = 0.0000e+00;
    P.finalPSFTotalMeanFluxEstimateRK = 0.0000e+00;
    P.finalPSFTotalMeanFluxEstimateG = 0.0000e+00;
    P.finalPSFTotalMeanFluxEstimateB = 0.0000e+00;
    P.finalPSFTotalMeanPowerFluxEstimateRK = 0.0000e+00;
    P.finalPSFTotalMeanPowerFluxEstimateG = 0.0000e+00;
    P.finalPSFTotalMeanPowerFluxEstimateB = 0.0000e+00;
    P.finalPSFMStarEstimateRK = 0.0000e+00;
    P.finalPSFMStarEstimateG = 0.0000e+00;
    P.finalPSFMStarEstimateB = 0.0000e+00;
    P.finalPSFNStarEstimateRK = 0.0000e+00;
    P.finalPSFNStarEstimateG = 0.0000e+00;
    P.finalPSFNStarEstimateB = 0.0000e+00;
    P.finalPSFCountRK = 0;
    P.finalPSFCountG = 0;
    P.finalPSFCountB = 0;
    P.referenceNoiseReductionRK = 0.0000;
    P.referenceNoiseReductionG = 0.0000;
    P.referenceNoiseReductionB = 0.0000;
    P.medianNoiseReductionRK = 0.0000;
    P.medianNoiseReductionG = 0.0000;
    P.medianNoiseReductionB = 0.0000;
    P.referenceSNRIncrementRK = 0.0000;
    P.referenceSNRIncrementG = 0.0000;
    P.referenceSNRIncrementB = 0.0000;
    P.averageSNRIncrementRK = 0.0000;
    P.averageSNRIncrementG = 0.0000;
    P.averageSNRIncrementB = 0.0000;
    P.imageData = [ // weightRK, weightG, weightB, rejectedLowRK, rejectedLowG, rejectedLowB, rejectedHighRK, rejectedHighG, rejectedHighB
    ];
     */
    return P;
}

function configureProcess(process, settings) {
    process.images = settings.inputFiles.map((file) => [true, file, "", ""]);
    process.weightMode = ImageIntegration.prototype.DontCare;
    process.normalization = ImageIntegration.prototype.Multiplicative;
    process.rejection = ImageIntegration.prototype.PercentileClip;
    process.rejectionNormalization = ImageIntegration.prototype.EqualizeFluxes;
    process.rangeClipLow = true;
    process.rangeClipHigh = true;
    process.generateRejectionMaps = false;
}

function main() {
    let settings = {
        outputFile: args[0],
        inputFiles: args.slice(1),
    };

    console.writeln();
    console.noteln("===== AutoFlats: Stack =====");
    console.writeln();
    console.writeln(JSON.stringify(settings, null, 2));

    let process = createProcess();

    configureProcess(process, settings);

    let processSuccess = false;
    let processLog = "";
    console.beginLog();
    try {
        processSuccess = process.executeGlobal();
    } finally {
        processLog = console.endLog().utf8ToString();
    }

    if (!processSuccess) {
        console.criticalln("*** Error: ImageIntegration process failed");
        throw new Error("ImageIntegration process failed: \n" + processLog);
    }

    let resultWindow = ImageWindow.activeWindow;
    if (resultWindow.mainView.id == "integration") {
        try {
            if (!resultWindow.saveAs(settings.outputFile, false, false, false, false)) {
                console.criticalln("*** Error: Failed saving to file " + settings.outputFile);
                throw new Error("Failed saving to file " + settings.outputFile);
            }
        } finally {
            resultWindow.forceClose();
        }
    } else {
        console.criticalln("*** Error: Unexpected view id " + resultWindow.mainView.id);
        throw new Error("Unexpected view id " + resultWindow.mainView.id);
    }
}

try {
    main();
    writeResult();
} catch (e) {
    writeResult("ERROR: " + e.toString());
}

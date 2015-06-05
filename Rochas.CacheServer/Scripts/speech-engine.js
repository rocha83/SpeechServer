
// by rocha83br - 2015

// vars
var leftchannel = [];
var rightchannel = [];
var recorder = null;
var recording = false;
var recordingLength = 0;
var volume = null;
var audioInput = null;
var sampleRate = null;
var audioContext = null;
var context = null;
var localStream = null;
var loopStream = null;

window.speech_engine = { };

window.speech_engine.init =
    function () {

        // Initialize user media
        if (!navigator.getUserMedia)
            navigator.getUserMedia = navigator.getUserMedia || navigator.webkitGetUserMedia ||
            navigator.mozGetUserMedia || navigator.msGetUserMedia;

        if (navigator.getUserMedia) {
            navigator.getUserMedia({ audio: true },

                function (stream) {

                    localStream = stream;

                    // Audio context
                    audioContext = window.AudioContext || window.webkitAudioContext;
                    context = new audioContext();

                    // Context sample rate (varies depending on platforms)
                    sampleRate = context.sampleRate;

                    console.log('success');

                    // Gain node
                    volume = context.createGain();

                    // Audio node from the microphone incoming stream
                    audioInput = context.createMediaStreamSource(localStream);

                    // Connect the stream to the gain node
                    audioInput.connect(volume);

                    /* Lower values for buffer size will result in a better latency. 
                       Higher values will be necessary to avoid audio breakup */
                    var bufferSize = 2048;
                    recorder = context.createScriptProcessor(bufferSize, 2, 2);

                    recorder.onaudioprocess = function (stream) {
                        if (!recording) return;

                        loopStream = stream;

                        var left = stream.inputBuffer.getChannelData(0);
                        var right = stream.inputBuffer.getChannelData(1);

                        // Samples clone
                        leftchannel.push(new Float32Array(left));
                        rightchannel.push(new Float32Array(right));
                        recordingLength += bufferSize;

                        console.log('recording');
                    }

                    // Recorder connection
                    volume.connect(recorder);
                    recorder.connect(context.destination);
                },
                function () {
                    alert('No audio detected.');
                });
        }
        else
            alert("Your browser don't support media stream.");
    };

window.speech_engine.getAudioBytes =
    function () {

        // Channes flat down
        var leftBuffer = mergeBuffers(leftchannel, recordingLength);
        var rightBuffer = mergeBuffers(rightchannel, recordingLength);

        // Channels interleave 
        var interleaved = interleave(leftBuffer, rightBuffer);

        // Wav buffer
        var buffer = new ArrayBuffer(44 + interleaved.length * 2);
        var view = new DataView(buffer);

        // RIFF chunk descriptor
        writeUTFBytes(view, 0, 'RIFF');
        view.setUint32(4, 44 + interleaved.length * 2, true);
        writeUTFBytes(view, 8, 'WAVE');

        // FMT sub-chunk
        writeUTFBytes(view, 12, 'fmt ');
        view.setUint32(16, 16, true);
        view.setUint16(20, 1, true);

        // Stereo (2 channels)
        view.setUint16(22, 2, true);
        view.setUint32(24, sampleRate, true);
        view.setUint32(28, sampleRate * 4, true);
        view.setUint16(32, 4, true);
        view.setUint16(34, 16, true);

        // Data sub-chunk
        writeUTFBytes(view, 36, 'data');
        view.setUint32(40, interleaved.length * 2, true);

        // PCM samples
        var lng = interleaved.length;
        var index = 44;
        var volume = 1;
        for (var i = 0; i < lng; i++) {
            view.setInt16(index, interleaved[i] * (0x7FFF * volume), true);
            index += 2;
        }

        // final binary blob
        var result = new Blob([view], { type: 'audio/wav' });

        return result;
    };

window.speech_engine.start = function () {

    var leftchannel = [];
    var rightchannel = [];
    var recorder = null;
    var recordingLength = 0;
    var volume = null;
    var audioInput = null;
    var sampleRate = null;
    var audioContext = null;
    var context = null;

    recording = true;

    window.speech_engine.init();
}

window.speech_engine.stop = function () {

    localStream.stop();
    recorder.disconnect();
    audioInput.disconnect();
    localStream = null;
    loopStream = null;
    audioInput = null;
    recorder = null;

    recording = false;
}

function interleave(leftChannel, rightChannel) {
    var length = leftChannel.length + rightChannel.length;
    var result = new Float32Array(length);

    var inputIndex = 0;

    for (var index = 0; index < length;) {
        result[index++] = leftChannel[inputIndex];
        result[index++] = rightChannel[inputIndex];
        inputIndex++;
    }
    return result;
}

function mergeBuffers(channelBuffer, recordingLength) {
    var result = new Float32Array(recordingLength);
    var offset = 0;
    var lng = channelBuffer.length;
    for (var i = 0; i < lng; i++) {
        var buffer = channelBuffer[i];
        result.set(buffer, offset);
        offset += buffer.length;
    }
    return result;
}

function writeUTFBytes(view, offset, string) {
    var lng = string.length;
    for (var i = 0; i < lng; i++) {
        view.setUint8(offset + i, string.charCodeAt(i));
    }
}

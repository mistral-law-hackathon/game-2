// Records the microphone with MediaRecorder, sends it to /api/stt (Mistral Voxtral via the server)
// and returns the transcript to the "Verdict" GameObject.
mergeInto(LibraryManager.library, {
  MicStart: function (goPtr) {
    var go = UTF8ToString(goPtr);
    var send = function (m, s) { (typeof SendMessage !== 'undefined' ? SendMessage : Module.SendMessage)(go, m, s); };
    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia || !window.MediaRecorder) {
      send('OnMicError', 'This browser cannot record audio.');
      return;
    }
    navigator.mediaDevices.getUserMedia({ audio: true }).then(function (stream) {
      var rec = new MediaRecorder(stream), chunks = [];
      window.verdictMic = rec;
      rec.ondataavailable = function (e) { if (e.data && e.data.size) chunks.push(e.data); };
      rec.onstop = function () {
        stream.getTracks().forEach(function (t) { t.stop(); });
        var blob = new Blob(chunks, { type: rec.mimeType || 'audio/webm' });
        fetch('/api/stt', { method: 'POST', headers: { 'Content-Type': blob.type }, body: blob })
          .then(function (r) { return r.json(); })
          .then(function (j) {
            if (j && typeof j.text === 'string' && !j.error) send('OnTranscript', j.text);
            else send('OnMicError', (j && j.error) || 'Transcription failed.');
          })
          .catch(function () { send('OnMicError', 'Transcription failed.'); });
      };
      rec.start();
      send('OnMicStarted', '');
    }).catch(function () { send('OnMicError', 'Microphone access was denied.'); });
  },
  MicStop: function () {
    var rec = window.verdictMic;
    if (rec && rec.state !== 'inactive') rec.stop();
  }
});

window.playSound = function (src, volume) {
    var audio = new Audio(src);
    audio.volume = (volume !== undefined && volume !== null) ? volume : 1.0;
    audio.play();
};

//function ready() {
//    if (document.readyState == 'complete') {
//        Webcam.set({
//            width: 320,
//            height: 240,
//            image_formate: 'jpeg',
//            quality: 90
//        });
//        try {
//            Webcam.attach('#camera');
//        }
//        catch (e) {
//            alert(e);
//        }
//    }
//}

//function take_snapshot() {
//    var data = null;
//    Webcam.snap(function(data_url) {
//        data = data_url;
//    }
//    return data;
//}
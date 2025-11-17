window.showCalendar = function () {
    var dateRangeWrapper = document.querySelector('.e-date-range-wrapper');

    if (dateRangeWrapper) {
        var rangeIcon = dateRangeWrapper.querySelector('.e-input-group-icon');

        if (rangeIcon) {
            var mouseUpEvent = new MouseEvent('mousedown', {
                bubbles: true,
                cancelable: true,
                view: window
            }); 
            rangeIcon.dispatchEvent(mouseUpEvent);
        } else {
            console.error('The span element with the specified class name was not found within the date range wrapper.');
        }
    } else {
        console.error('The date range wrapper element was not found.');
    }

}

window.triggerClick = function (inputFile) {
    document.querySelector('input[type="file"]').click();
}

function openModal() {
    var modal = document.getElementById("custom-modal");
    var modalLayer = document.getElementById("modal-overlay")
    modal.style.display = "block";
    modalLayer.style.display = "block";
}

// Function to close the modal
function closeModal() {
    var modal = document.getElementById("custom-modal");
    var modalLayer = document.getElementById("modal-overlay")
    modalLayer.style.display = "none";
    modal.style.display = "none";

    var cameraDiv = document.getElementById('camera');
    cameraDiv.style.width = '0px';
    cameraDiv.style.height = '0px';
    try {
        Webcam.reset();
    }
    catch (e) {
        alert(e);
    }
}

function ShowCamera(value) {
    if (document.readyState == 'complete') {
        if (value == 0) {
            if (isCameraAttached() == true) {
                var cameraDiv1 = document.getElementById('camera');
                cameraDiv1.style.width = '0px';
                cameraDiv1.style.height = '0px';
                try {
                    Webcam.reset();
                }
                catch (e) {
                    alert(e);
                }
            }
        }
        else {
            if (isCameraAttached() == true) {
                var cameraDiv = document.getElementById('camera');
                cameraDiv.style.width = '0px';
                cameraDiv.style.height = '0px';
                try {
                    Webcam.reset();
                }
                catch (e) {
                    alert(e);
                }
            }
            else {
                Webcam.set({
                    height: 200,
                    width: 300,
                    image_formate: 'jpeg',
                    quality: 90
                });
                var cameraDiv = document.getElementById('camera');

                if (cameraDiv.style.width == '0px') {
                    cameraDiv.style.width = '300px';
                    cameraDiv.style.height = '200px';
                    var cameraComponents = document.getElementById('CameraContent');
                    cameraComponents.style.display = 'block';
                }
                cameraDiv.style.margin='auto';
               
                try {
                    Webcam.attach('#camera');
                }
                catch (e) {
                    alert(e);
                }
            }
        }
    }
} 

function CameraNotFound() {
    var cameraDiv = document.getElementById('camera');
    cameraDiv.style.width = '0px';
    cameraDiv.style.height = '0px';
    var cameraComponents = document.getElementById('CameraContent');
    cameraComponents.style.display = 'none';
}

function isCameraAttached() {
    var isAttached = typeof Webcam !== 'undefined' && typeof Webcam.stream !== 'undefined'; 
    return isAttached;
}

function take_snapshot() {
    var data = null;
    Webcam.snap(function (data_url) {
        data = data_url;
    });
    return data;
}





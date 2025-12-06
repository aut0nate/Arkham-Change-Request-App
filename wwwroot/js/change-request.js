// Email validation functionality
function validateEmail(email) {
    const emailRegex = /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/;
    return emailRegex.test(email);
}

function showEmailFeedback(isValid, message) {
    const feedback = document.getElementById('emailFeedback');
    const emailInput = document.getElementById('emailInput');
    
    feedback.textContent = message;
    
    if (isValid) {
        feedback.className = 'email-validation-feedback valid';
        emailInput.classList.remove('invalid');
        emailInput.classList.add('valid');
    } else {
        feedback.className = 'email-validation-feedback invalid';
        emailInput.classList.remove('valid');
        emailInput.classList.add('invalid');
    }
}

// File upload functionality
function formatLocalDateTime(date) {
    const offsetDate = new Date(date.getTime() - date.getTimezoneOffset() * 60000);
    return offsetDate.toISOString().slice(0, 16);
}

document.addEventListener('DOMContentLoaded', function() {
    // Email validation
    const emailInput = document.getElementById('emailInput');
    const implementationInput = document.getElementById('ProposedImplementationDate');
    
    if (emailInput) {
        emailInput.addEventListener('input', function() {
            const email = this.value.trim();
            
            if (email === '') {
                showEmailFeedback(false, '');
                return;
            }
            
            if (validateEmail(email)) {
                showEmailFeedback(true, '✓ Valid email address');
            } else {
                showEmailFeedback(false, '✗ Please enter a valid email address');
            }
        });

        emailInput.addEventListener('blur', function() {
            const email = this.value.trim();
            
            if (email !== '' && !validateEmail(email)) {
                showEmailFeedback(false, '✗ Please enter a valid email address (e.g., user@company.com)');
            }
        });
    }

    if (implementationInput) {
        const nowString = formatLocalDateTime(new Date());
        implementationInput.setAttribute('min', nowString);

        if (!implementationInput.value || new Date(implementationInput.value) < new Date()) {
            implementationInput.value = nowString;
        }
    }

    // File upload functionality
    const fileUploadArea = document.getElementById('fileUploadArea');
    const fileInput = document.getElementById('fileInput');
    const fileList = document.getElementById('fileList');

    if (fileUploadArea && fileInput && fileList) {
        fileUploadArea.addEventListener('click', () => fileInput.click());
        
        fileUploadArea.addEventListener('dragover', (e) => {
            e.preventDefault();
            fileUploadArea.classList.add('drag-over');
        });

        fileUploadArea.addEventListener('dragleave', () => {
            fileUploadArea.classList.remove('drag-over');
        });

        fileUploadArea.addEventListener('drop', (e) => {
            e.preventDefault();
            fileUploadArea.classList.remove('drag-over');
            handleFiles(e.dataTransfer.files);
        });

        fileInput.addEventListener('change', (e) => {
            handleFiles(e.target.files);
        });
    }

    function handleFiles(files) {
        if (fileList) {
            fileList.innerHTML = '';
            Array.from(files).forEach(file => {
                const fileItem = document.createElement('div');
                fileItem.className = 'file-item';
                fileItem.innerHTML = `
                    <span class="file-name">${file.name}</span>
                    <span class="file-size">(${(file.size / 1024 / 1024).toFixed(2)} MB)</span>
                `;
                fileList.appendChild(fileItem);
            });
        }
    }

    // Form submission validation
    const form = document.querySelector('.change-request-form');
    if (form && emailInput) {
        form.addEventListener('submit', function(e) {
            const email = emailInput.value.trim();
            
            if (!validateEmail(email)) {
                e.preventDefault();
                showEmailFeedback(false, '✗ Please enter a valid email address before submitting');
                emailInput.focus();
                return false;
            }
        });
    }
});

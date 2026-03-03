// Query editor JavaScript functions

function updatePreview(textarea) {
    var preview = document.getElementById('query-preview');
    if (preview) {
        preview.textContent = textarea.value;
        if (window.Prism) {
            window.Prism.highlightElement(preview);
        }
    }
}

async function testQuery() {
    var queryText = document.getElementById('query').value.trim();
    var statusEl = document.getElementById('test-status');
    var resultsDiv = document.getElementById('test-results');
    var contentDiv = document.getElementById('results-content');
    
    if (!queryText) {
        statusEl.textContent = 'Please enter a query';
        statusEl.style.color = '#ff0000';
        return;
    }
    
    statusEl.textContent = 'Testing...';
    statusEl.style.color = '#0000ff';
    resultsDiv.style.display = 'none';
    
    try {
        var formData = new FormData();
        formData.append('query', queryText);
        
        var response = await fetch('/queries/test', {
            method: 'POST',
            body: formData
        });
        
        var data = await response.json();
        
        if (data.success) {
            statusEl.textContent = 'Success! Found ' + data.items.length + ' results';
            statusEl.style.color = '#008000';
            
            if (data.items.length > 0) {
                renderResults(data.items, contentDiv);
                resultsDiv.style.display = 'block';
            } else {
                contentDiv.innerHTML = '<p>No results found.</p>';
                resultsDiv.style.display = 'block';
            }
        } else {
            statusEl.textContent = 'Error: ' + data.error;
            statusEl.style.color = '#ff0000';
            resultsDiv.style.display = 'none';
        }
    } catch (err) {
        statusEl.textContent = 'Error: ' + err.message;
        statusEl.style.color = '#ff0000';
        resultsDiv.style.display = 'none';
    }
}

function renderResults(items, container) {
    if (!items || items.length === 0) {
        container.innerHTML = '<p>No results found.</p>';
        return;
    }
    
    var keys = Object.keys(items[0]);
    
    var html = '<table>';
    html += '<thead><tr>';
    keys.forEach(function(key) {
        html += '<th>' + key + '</th>';
    });
    html += '</tr></thead><tbody>';
    
    var displayItems = items.slice(0, 100);
    displayItems.forEach(function(item) {
        html += '<tr>';
        keys.forEach(function(key) {
            var value = item[key];
            if (value === null || value === undefined) {
                value = '<em style="color: #999;">null</em>';
            } else if (Array.isArray(value)) {
                value = value.join(', ');
            } else {
                value = String(value);
            }
            html += '<td>' + value + '</td>';
        });
        html += '</tr>';
    });
    
    html += '</tbody></table>';
    
    if (items.length > 100) {
        html += '<p style="margin-top: 10px; color: #666;"><em>Showing first 100 of ' + items.length + ' results</em></p>';
    }
    
    container.innerHTML = html;
}

// Initialize preview on page load
document.addEventListener('DOMContentLoaded', function() {
    var textarea = document.getElementById('query');
    if (textarea) {
        updatePreview(textarea);
    }
});

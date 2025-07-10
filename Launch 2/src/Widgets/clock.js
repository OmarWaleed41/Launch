const days = ["Sun","Mon","Tue","Wed","Thu","Fri","Sat"];
const months = ["Jan", "Feb", "Mar", "April", "May", "June", "July", "Aug", "Sep", "Oct", "Nov", "Dec"];

const body = document.body;

const clock = document.createElement('div');
const hours = document.createElement('div');
const minutes = document.createElement('div');
const period = document.createElement('div');

clock.className = 'clock';
clock.id = 'clock';

hours.className = 'hours';
hours.id = 'hours';

minutes.className = 'minutes';
minutes.id = 'minutes';

period.className = 'period';
period.id = 'period';
clock.appendChild(hours);
clock.appendChild(minutes);
clock.appendChild(period);
body.appendChild(clock);

function updateClock() {
    const now = new Date();
    const currentHour = (now.getHours() % 12 || 12).toString().padStart(2, '0');
    const CurrentMinutes = now.getMinutes().toString().padStart(2, '0');
    const currentPeriod = now.getHours() >= 12 ? "PM" : "AM";
    
    hours.textContent = `${currentHour}`;
    minutes.textContent = `${CurrentMinutes}`;
    period.textContent = `${currentPeriod}  ${days[now.getDay()]} - ${now.getDate()} ${months[now.getMonth()]}`;
}

setInterval(updateClock, 1000);
updateClock();
